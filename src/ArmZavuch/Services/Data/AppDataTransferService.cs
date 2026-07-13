using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using ArmZavuch.Data;
using ArmZavuch.Data.Migrations;
using ArmZavuch.Models;
using ArmZavuch.Services.Recovery;
using ArmZavuch.Services.Save;
using ArmZavuch.Services.Settings;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Data;

/// <summary>Полная выгрузка и загрузка данных приложения для переноса между компьютерами.</summary>
public sealed class AppDataTransferService
{
    public const string FileExtension = ".armzavuch";
    public const int CurrentFormatVersion = 2;
    private const string ManifestEntry = "manifest.json";
    private const string DatabaseEntry = "school.db";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SqliteConnectionFactory _factory;
    private readonly MigrationRunner _migrations;
    private readonly AppSettingsService _settings;
    private readonly ISaveStateService _saveState;
    private readonly IRecoveryService _recovery;
    private readonly IAppDataChangeNotifier _dataChangeNotifier;
    private readonly AppDataSelectiveImporter _selectiveImporter;

    public AppDataTransferService(
        SqliteConnectionFactory factory,
        MigrationRunner migrations,
        AppSettingsService settings,
        ISaveStateService saveState,
        IRecoveryService recovery,
        IAppDataChangeNotifier dataChangeNotifier,
        AppDataSelectiveImporter selectiveImporter)
    {
        _factory = factory;
        _migrations = migrations;
        _settings = settings;
        _saveState = saveState;
        _recovery = recovery;
        _dataChangeNotifier = dataChangeNotifier;
        _selectiveImporter = selectiveImporter;
    }

    public static string SuggestFileName(string schoolName)
    {
        var safe = string.Concat((schoolName ?? "Школа").Where(ch =>
            !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
        if (string.IsNullOrWhiteSpace(safe))
            safe = "Школа";
        return $"Расписание_Про_{safe}_{DateTime.Now:yyyy-MM-dd}{FileExtension}";
    }

    public async Task<AppDataTransferManifest?> ReadManifestAsync(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(ManifestEntry);
            if (entry is null)
                return null;

            await using var stream = entry.Open();
            return await JsonSerializer.DeserializeAsync<AppDataTransferManifest>(stream);
        }
        catch
        {
            return null;
        }
    }

    public async Task<AppDataTransferResult> ExportAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return AppDataTransferResult.Fail("Не указан путь к файлу.");

        try
        {
            if (_saveState.IsDirty)
                await _saveState.SaveAsync();

            var tempDir = Path.Combine(Path.GetTempPath(), "ArmZavuch-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempDb = Path.Combine(tempDir, DatabaseEntry);

            try
            {
                await using var connection = _factory.CreateConnection();
                await CheckpointDatabaseAsync(connection);
                var schemaVersion = await ReadSchemaVersionAsync(connection);
                var schoolName = await ReadSchoolNameAsync(connection);
                await CreateConsistentDatabaseCopyAsync(connection, tempDb);

                var manifest = new AppDataTransferManifest
                {
                    FormatVersion = CurrentFormatVersion,
                    SchemaVersion = schemaVersion,
                    AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                    ExportedAt = DateTime.UtcNow,
                    SchoolName = schoolName,
                    ExportedSections = []
                };

                var manifestPath = Path.Combine(tempDir, ManifestEntry);
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                SqliteConnection.ClearAllPools();
                ZipFile.CreateFromDirectory(tempDir, archivePath);
                return AppDataTransferResult.Ok(
                    $"Все данные выгружены в файл «{Path.GetFileName(archivePath)}».",
                    manifest);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }
        catch (Exception ex)
        {
            return AppDataTransferResult.Fail($"Не удалось выгрузить данные: {ex.Message}");
        }
    }

    public async Task<AppDataTransferResult> ExportSelectiveAsync(
        string archivePath,
        IReadOnlyList<AppDataTransferSection> sections)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return AppDataTransferResult.Fail("Не указан путь к файлу.");

        if (sections.Count == 0)
            return AppDataTransferResult.Fail("Не выбран ни один раздел для выгрузки.");

        try
        {
            if (_saveState.IsDirty)
                await _saveState.SaveAsync();

            var tempDir = Path.Combine(Path.GetTempPath(), "ArmZavuch-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempDb = Path.Combine(tempDir, DatabaseEntry);

            try
            {
                await using var connection = _factory.CreateConnection();
                await CheckpointDatabaseAsync(connection);
                var schemaVersion = await ReadSchemaVersionAsync(connection);
                var schoolName = await ReadSchoolNameAsync(connection);
                await CreateConsistentDatabaseCopyAsync(connection, tempDb);

                var manifest = new AppDataTransferManifest
                {
                    FormatVersion = CurrentFormatVersion,
                    SchemaVersion = schemaVersion,
                    AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                    ExportedAt = DateTime.UtcNow,
                    SchoolName = schoolName,
                    ExportedSections = sections.Select(s => s.ToString()).Distinct().ToList()
                };

                var manifestPath = Path.Combine(tempDir, ManifestEntry);
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                SqliteConnection.ClearAllPools();
                ZipFile.CreateFromDirectory(tempDir, archivePath);

                var names = string.Join(", ", sections.Select(AppDataSectionCatalog.Title));
                return AppDataTransferResult.Ok(
                    $"Выгружены разделы: {names}. Файл «{Path.GetFileName(archivePath)}».",
                    manifest);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }
        catch (Exception ex)
        {
            return AppDataTransferResult.Fail($"Не удалось выгрузить данные: {ex.Message}");
        }
    }

    public async Task<AppDataTransferResult> ImportSelectiveAsync(
        string archivePath,
        IReadOnlyList<AppDataTransferSection> sections,
        AppDataImportMode mode)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return AppDataTransferResult.Fail("Файл не найден.");

        if (sections.Count == 0)
            return AppDataTransferResult.Fail("Не выбран ни один раздел для загрузки.");

        var tempDir = Path.Combine(Path.GetTempPath(), "ArmZavuch-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(archivePath, tempDir);

            var manifestPath = Path.Combine(tempDir, ManifestEntry);
            var sourceDb = Path.Combine(tempDir, DatabaseEntry);
            if (!File.Exists(manifestPath) || !File.Exists(sourceDb))
                return AppDataTransferResult.Fail("Неверный формат архива: нужны manifest.json и school.db.");

            var manifest = JsonSerializer.Deserialize<AppDataTransferManifest>(
                await File.ReadAllTextAsync(manifestPath));
            if (manifest is null || !string.Equals(manifest.Format, "armzavuch-backup", StringComparison.OrdinalIgnoreCase))
                return AppDataTransferResult.Fail($"Это не архив данных {AppBranding.ProductName}.");

            if (manifest.FormatVersion > CurrentFormatVersion)
            {
                return AppDataTransferResult.Fail(
                    $"Архив создан более новой версией программы. Обновите {AppBranding.ProductName} и повторите загрузку.");
            }

            if (manifest.SchemaVersion > _migrations.LatestSchemaVersion)
            {
                return AppDataTransferResult.Fail(
                    $"Архив использует схему данных v{manifest.SchemaVersion}, а программа поддерживает до v{_migrations.LatestSchemaVersion}. " +
                    "Обновите приложение.");
            }

            if (!IsValidSqliteDatabase(sourceDb))
                return AppDataTransferResult.Fail("Файл базы данных в архиве повреждён или не читается.");

            var importDb = Path.Combine(tempDir, "school_import.db");
            await CreateImportDatabaseSnapshotAsync(sourceDb, importDb);
            if (!File.Exists(importDb) || new FileInfo(importDb).Length == 0)
                return AppDataTransferResult.Fail("Не удалось подготовить копию базы из архива для импорта.");

            var available = manifest.ResolveExportedSections().ToHashSet();
            var unavailable = sections.Where(s => !available.Contains(s)).ToList();
            if (unavailable.Count > 0)
            {
                var names = string.Join(", ", unavailable.Select(AppDataSectionCatalog.Title));
                return AppDataTransferResult.Fail(
                    $"В архиве нет разделов: {names}. Выберите только те разделы, которые были выгружены, или используйте полный архив.");
            }

            var result = await _selectiveImporter.ImportAsync(importDb, sections, mode);
            if (!result.Success)
                return result;

            await _recovery.DiscardDraftAsync();
            await _saveState.SaveAsync();
            _dataChangeNotifier.NotifyDataChanged();

            return AppDataTransferResult.Ok(result.Message, manifest);
        }
        catch (Exception ex)
        {
            return AppDataTransferResult.Fail($"Не удалось загрузить данные: {ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public async Task<AppDataTransferResult> ImportAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return AppDataTransferResult.Fail("Файл не найден.");

        var tempDir = Path.Combine(Path.GetTempPath(), "ArmZavuch-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(archivePath, tempDir);

            var manifestPath = Path.Combine(tempDir, ManifestEntry);
            var sourceDb = Path.Combine(tempDir, DatabaseEntry);
            if (!File.Exists(manifestPath) || !File.Exists(sourceDb))
                return AppDataTransferResult.Fail("Неверный формат архива: нужны manifest.json и school.db.");

            var manifest = JsonSerializer.Deserialize<AppDataTransferManifest>(
                await File.ReadAllTextAsync(manifestPath));
            if (manifest is null || !string.Equals(manifest.Format, "armzavuch-backup", StringComparison.OrdinalIgnoreCase))
                return AppDataTransferResult.Fail($"Это не архив данных {AppBranding.ProductName}.");

            if (manifest.FormatVersion > CurrentFormatVersion)
            {
                return AppDataTransferResult.Fail(
                    $"Архив создан более новой версией программы. Обновите {AppBranding.ProductName} и повторите загрузку.");
            }

            if (manifest.SchemaVersion > _migrations.LatestSchemaVersion)
            {
                return AppDataTransferResult.Fail(
                    $"Архив использует схему данных v{manifest.SchemaVersion}, а программа поддерживает до v{_migrations.LatestSchemaVersion}. " +
                    "Обновите приложение.");
            }

            if (!IsValidSqliteDatabase(sourceDb))
                return AppDataTransferResult.Fail("Файл базы данных в архиве повреждён или не читается.");

            RecoveryDatabaseHelper.ReplaceDatabase(_factory.DatabasePath, sourceDb);
            await _recovery.DiscardDraftAsync();
            await _migrations.RunAsync();
            await _settings.LoadAsync();
            await _saveState.SaveAsync();
            _dataChangeNotifier.NotifyDataChanged();

            var school = string.IsNullOrWhiteSpace(manifest.SchoolName) ? _settings.SchoolName : manifest.SchoolName;
            return AppDataTransferResult.Ok(
                $"Данные школы «{school}» загружены. Справочники, расписание, календарь и замены перенесены.",
                manifest);
        }
        catch (Exception ex)
        {
            return AppDataTransferResult.Fail($"Не удалось загрузить данные: {ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static async Task CheckpointDatabaseAsync(SqliteConnection connection)
    {
        await using var checkpoint = connection.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await checkpoint.ExecuteNonQueryAsync();
    }

    private static async Task CreateConsistentDatabaseCopyAsync(SqliteConnection sourceConnection, string targetPath)
    {
        DeleteDatabaseSidecars(targetPath);

        await using var destination = OpenDatabase(targetPath);
        sourceConnection.BackupDatabase(destination);
        await destination.CloseAsync();
        SqliteConnection.ClearAllPools();
    }

    private static async Task<int> ReadSchemaVersionAsync(SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = 'schema_version'";
        var result = await cmd.ExecuteScalarAsync();
        return result is string s && int.TryParse(s, out var version) ? version : 0;
    }

    private async Task<string> ReadSchoolNameAsync(SqliteConnection connection)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k";
            cmd.Parameters.AddWithValue("$k", AppSettingsService.SchoolNameKey);
            var result = await cmd.ExecuteScalarAsync();
            return result is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : _settings.SchoolName;
        }
        catch (SqliteException)
        {
            return _settings.SchoolName;
        }
    }

    private static async Task CreateImportDatabaseSnapshotAsync(string sourceDb, string targetDb)
    {
        SqliteConnection.ClearAllPools();
        DeleteDatabaseSidecars(targetDb);
        if (File.Exists(targetDb))
            File.Delete(targetDb);

        await using (var source = OpenDatabase(sourceDb))
        {
            await using var checkpoint = source.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync();

            await using var destination = new SqliteConnection($"Data Source={targetDb};Pooling=false");
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        SqliteConnection.ClearAllPools();
    }

    private static bool IsValidSqliteDatabase(string dbPath)
    {
        try
        {
            using var connection = OpenDatabase(dbPath);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' LIMIT 1";
            return cmd.ExecuteScalar() is not null;
        }
        catch
        {
            return false;
        }
    }

    private static SqliteConnection OpenDatabase(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath};Pooling=false");
        connection.Open();
        return connection;
    }

    private static void DeleteDatabaseSidecars(string dbPath)
    {
        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // temp cleanup best-effort
        }
    }
}
