using ArmZavuch.Data;
using ArmZavuch.Models;
using ArmZavuch.Services.Recovery;
using ArmZavuch.Services.Save;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Data;

/// <summary>Удаляет пользовательские данные из БД целиком или по разделам справочника.</summary>
public sealed class DatabaseClearService
{
    private static readonly string[] AllDataTables =
    [
        "substitution_records",
        "day_overrides",
        "week_template_slots",
        "week_template_auto_snapshots",
        "calendar_exceptions",
        "schedule_periods",
        "week_templates",
        "teacher_curriculum_items",
        "curriculum",
        "teacher_building_days",
        "teacher_preferred_classes",
        "teacher_status_periods",
        "teacher_unavailability",
        "teacher_subjects",
        "class_teachers",
        "rooms",
        "teachers",
        "building_routes",
        "school_classes",
        "subjects",
        "buildings"
    ];

    private readonly SqliteConnectionFactory _factory;
    private readonly ISaveStateService _saveState;
    private readonly IRecoveryService _recovery;
    private readonly IAppDataChangeNotifier _dataChangeNotifier;

    public DatabaseClearService(
        SqliteConnectionFactory factory,
        ISaveStateService saveState,
        IRecoveryService recovery,
        IAppDataChangeNotifier dataChangeNotifier)
    {
        _factory = factory;
        _saveState = saveState;
        _recovery = recovery;
        _dataChangeNotifier = dataChangeNotifier;
    }

    public async Task ClearAllAsync()
    {
        await RunClearAsync(async (conn, tx) =>
        {
            foreach (var table in AllDataTables)
                await DeleteTableAsync(conn, tx, table);

            await ClearCustomCurriculumTemplatesAsync(conn, tx);
            await ClearCustomBellTemplatesAsync(conn, tx);

            await ExecuteAsync(conn, tx, """
                DELETE FROM app_settings
                WHERE key <> 'school_name'
                  AND key NOT LIKE 'bell.default.%'
                """);

            await ResetSequencesAsync(conn, tx,
            [
                "buildings", "building_routes", "subjects", "school_classes", "rooms", "teachers",
                "curriculum", "teacher_curriculum_items", "week_templates", "week_template_auto_snapshots",
                "schedule_periods", "week_template_slots", "calendar_exceptions", "day_overrides",
                "substitution_records", "teacher_status_periods", "teacher_unavailability", "teacher_building_days"
            ]);
        });

        await using var conn = _factory.CreateConnection();
        await CurriculumTemplateSeed.EnsureBuiltInAsync(conn);
        await BellTemplateSeed.EnsureBuiltInAsync(conn);
        _dataChangeNotifier.NotifyDataChanged();
    }

    public Task ClearCurriculumAsync(CurriculumClearMode mode) =>
        RunClearAsync(async (conn, tx) => await ClearCurriculumTablesAsync(conn, tx, mode));

    public Task ClearSectionAsync(DirectoryClearSection section) =>
        RunClearAsync(async (conn, tx) =>
        {
            switch (section)
            {
                case DirectoryClearSection.Buildings:
                    await DeleteTableAsync(conn, tx, "week_template_slots");
                    await DeleteTableAsync(conn, tx, "day_overrides");
                    await DeleteTableAsync(conn, tx, "substitution_records");
                    await ExecuteAsync(conn, tx, "UPDATE teachers SET room_id = NULL WHERE room_id IS NOT NULL");
                    await DeleteTableAsync(conn, tx, "rooms");
                    await DeleteTableAsync(conn, tx, "building_routes");
                    await DeleteTableAsync(conn, tx, "buildings");
                    await ResetSequencesAsync(conn, tx,
                        ["buildings", "building_routes", "rooms", "week_template_slots", "day_overrides", "substitution_records"]);
                    break;

                case DirectoryClearSection.Subjects:
                    await DeleteTableAsync(conn, tx, "week_template_slots");
                    await DeleteTableAsync(conn, tx, "teacher_curriculum_items");
                    await DeleteTableAsync(conn, tx, "curriculum");
                    await DeleteTableAsync(conn, tx, "teacher_subjects");
                    await DeleteTableAsync(conn, tx, "subjects");
                    await ResetSequencesAsync(conn, tx, ["subjects", "curriculum", "week_template_slots"]);
                    break;

                case DirectoryClearSection.Classes:
                    await DeleteTableAsync(conn, tx, "week_template_slots");
                    await ExecuteAsync(conn, tx, "DELETE FROM day_overrides WHERE class_id IS NOT NULL");
                    await ExecuteAsync(conn, tx, "DELETE FROM substitution_records WHERE class_id IS NOT NULL");
                    await DeleteTableAsync(conn, tx, "teacher_curriculum_items");
                    await DeleteTableAsync(conn, tx, "curriculum");
                    await DeleteTableAsync(conn, tx, "class_teachers");
                    await DeleteTableAsync(conn, tx, "teacher_preferred_classes");
                    await DeleteTableAsync(conn, tx, "school_classes");
                    await ResetSequencesAsync(conn, tx, ["school_classes", "curriculum", "week_template_slots"]);
                    break;

                case DirectoryClearSection.Teachers:
                    await DeleteTableAsync(conn, tx, "week_template_slots");
                    await DeleteTableAsync(conn, tx, "day_overrides");
                    await DeleteTableAsync(conn, tx, "substitution_records");
                    await DeleteTableAsync(conn, tx, "teacher_curriculum_items");
                    await DeleteTableAsync(conn, tx, "teacher_building_days");
                    await DeleteTableAsync(conn, tx, "teacher_status_periods");
                    await DeleteTableAsync(conn, tx, "teacher_unavailability");
                    await DeleteTableAsync(conn, tx, "teacher_subjects");
                    await DeleteTableAsync(conn, tx, "class_teachers");
                    await DeleteTableAsync(conn, tx, "teacher_preferred_classes");
                    await ExecuteAsync(conn, tx, "UPDATE rooms SET assigned_teacher_id = NULL WHERE assigned_teacher_id IS NOT NULL");
                    await DeleteTableAsync(conn, tx, "teachers");
                    await ResetSequencesAsync(conn, tx,
                    [
                        "teachers", "teacher_status_periods", "teacher_unavailability", "teacher_building_days",
                        "week_template_slots", "day_overrides", "substitution_records"
                    ]);
                    break;

                case DirectoryClearSection.Rooms:
                    await DeleteTableAsync(conn, tx, "week_template_slots");
                    await ExecuteAsync(conn, tx, "DELETE FROM day_overrides WHERE room_id IS NOT NULL");
                    await ExecuteAsync(conn, tx, "UPDATE teachers SET room_id = NULL WHERE room_id IS NOT NULL");
                    await ExecuteAsync(conn, tx, "UPDATE rooms SET assigned_teacher_id = NULL WHERE assigned_teacher_id IS NOT NULL");
                    await DeleteTableAsync(conn, tx, "rooms");
                    await ResetSequencesAsync(conn, tx, ["rooms", "week_template_slots"]);
                    break;

                case DirectoryClearSection.Curriculum:
                    await ClearCurriculumTablesAsync(conn, tx, CurriculumClearMode.All);
                    break;

                case DirectoryClearSection.Bells:
                    await ExecuteAsync(conn, tx, "UPDATE day_overrides SET bell_template_id = NULL WHERE bell_template_id IS NOT NULL");
                    await ClearCustomBellTemplatesAsync(conn, tx);
                    await ResetSequencesAsync(conn, tx, ["bell_templates", "bell_periods"]);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, null);
            }
        });

    private static async Task ClearCustomCurriculumTemplatesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        await ExecuteAsync(conn, tx, """
            DELETE FROM curriculum_template_items
            WHERE template_id IN (SELECT id FROM curriculum_templates WHERE is_builtin = 0)
            """);
        await ExecuteAsync(conn, tx, "DELETE FROM curriculum_templates WHERE is_builtin = 0");
    }

    private static async Task ClearCustomBellTemplatesAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        var names = BuiltInDataCatalog.SqlBellNamesInList();
        await ExecuteAsync(conn, tx, $"""
            DELETE FROM bell_periods
            WHERE template_id IN (SELECT id FROM bell_templates WHERE name NOT IN ({names}))
            """);
        await ExecuteAsync(conn, tx, $"DELETE FROM bell_templates WHERE name NOT IN ({names})");
    }

    private static async Task ClearCurriculumTablesAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        CurriculumClearMode mode)
    {
        if (mode == CurriculumClearMode.TeacherAssignmentsOnly)
        {
            await DeleteTableAsync(conn, tx, "teacher_curriculum_items");
            return;
        }

        await DeleteTableAsync(conn, tx, "teacher_curriculum_items");
        await DeleteTableAsync(conn, tx, "curriculum");
        await ResetSequencesAsync(conn, tx, ["curriculum"]);
    }

    private async Task RunClearAsync(Func<SqliteConnection, SqliteTransaction, Task> clearAction)
    {
        SqliteConnection.ClearAllPools();

        await using var conn = _factory.CreateConnection();
        await ExecuteAsync(conn, null, "PRAGMA foreign_keys = OFF");

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
        try
        {
            await clearAction(conn, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await ExecuteAsync(conn, null, "PRAGMA foreign_keys = ON");

        await _recovery.DiscardDraftAsync();
        await _saveState.SaveAsync();
        _dataChangeNotifier.NotifyDataChanged();
    }

    private static async Task DeleteTableAsync(SqliteConnection conn, SqliteTransaction tx, string table) =>
        await ExecuteAsync(conn, tx, $"DELETE FROM {table}");

    private static async Task ExecuteAsync(SqliteConnection conn, SqliteTransaction? tx, string sql)
    {
        await using var cmd = conn.CreateCommand();
        if (tx is not null)
            cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ResetSequencesAsync(SqliteConnection conn, SqliteTransaction tx, IEnumerable<string> tables)
    {
        var names = string.Join(", ", tables.Select(t => $"'{t}'"));
        await ExecuteAsync(conn, tx, $"DELETE FROM sqlite_sequence WHERE name IN ({names})");
    }
}
