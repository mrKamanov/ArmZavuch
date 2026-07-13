using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>
/// Применяет миграции по порядку версий. Хранит текущую версию в meta.schema_version.
/// </summary>
public sealed class MigrationRunner
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IMigration[] _migrations;

    public MigrationRunner(SqliteConnectionFactory factory)
    {
        _factory = factory;
        _migrations =
        [
            new Migration001Initial(),
            new Migration002StaffAndAvailability(),
            new Migration003WeekParity(),
            new Migration004CurriculumParityKey(),
            new Migration005BellGradeProfiles(),
            new Migration006TeacherClassPreferences(),
            new Migration007DayOverrideTargets(),
            new Migration008ClassCorrectional(),
            new Migration009ClassDefaultRoom(),
            new Migration010PartialSlots(),
            new Migration011TeacherBuildingDays(),
            new Migration012NullableTeacherSlot(),
            new Migration013PeRoomAndHallSharing(),
            new Migration014TeacherClassSubjects(),
            new Migration015TeacherCurriculumItems(),
            new Migration016RoomKindRegularRu(),
            new Migration017ClassBuilding(),
            new Migration018CurriculumDifficulty(),
            new Migration019CurriculumTemplates(),
            new Migration020CurriculumTemplatesPerGrade(),
            new Migration021CurriculumTemplatesGrade789(),
            new Migration022BellTemplateRussianNames(),
            new Migration023BellTemplateAssignments(),
            new Migration024TeacherCurriculumSource(),
            new Migration025TeacherPreferredClassSource(),
            new Migration026RepairLegacyTeacherCurriculum(),
            new Migration027WeekTemplateAutoSnapshot(),
            new Migration028ScheduleSlotAnchors(),
            new Migration029TeacherAbsenceUnify(),
            new Migration030SubstitutionLedger(),
            new Migration031SubstitutionRecordShift(),
            new Migration032RemoveCustomRecurrenceCycle(),
            new Migration033BellGrade1SecondHalf()
        ];
    }

    public int LatestSchemaVersion => _migrations.Max(m => m.Version);

    public async Task RunAsync()
    {
        await using var connection = _factory.CreateConnection();
        await EnsureMetaTableAsync(connection);

        var current = await GetCurrentVersionAsync(connection);
        foreach (var migration in _migrations.OrderBy(m => m.Version))
        {
            if (migration.Version <= current)
                continue;

            await migration.ApplyAsync(_factory);
            await SetVersionAsync(connection, migration.Version);
        }
    }

    private static async Task EnsureMetaTableAsync(SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = 'schema_version'";
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? 0 : int.Parse((string)result);
    }

    private static async Task SetVersionAsync(SqliteConnection connection, int version)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO meta (key, value) VALUES ('schema_version', $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$v", version.ToString());
        await cmd.ExecuteNonQueryAsync();
    }
}
