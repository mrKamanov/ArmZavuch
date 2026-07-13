using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>
/// Начальная схема: справочники, шаблоны недели, календарь, оперативные правки.
/// </summary>
public sealed class Migration001Initial : IMigration
{
    public int Version => 1;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var connection = factory.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS buildings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                color_hex TEXT NOT NULL DEFAULT '#2563EB'
            );

            CREATE TABLE IF NOT EXISTS building_routes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                from_building_id INTEGER NOT NULL REFERENCES buildings(id),
                to_building_id INTEGER NOT NULL REFERENCES buildings(id),
                minutes INTEGER NOT NULL,
                UNIQUE(from_building_id, to_building_id)
            );

            CREATE TABLE IF NOT EXISTS subjects (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                difficulty_score REAL NOT NULL DEFAULT 1.0
            );

            CREATE TABLE IF NOT EXISTS school_classes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                grade INTEGER NOT NULL,
                letter TEXT NOT NULL,
                shift INTEGER NOT NULL DEFAULT 1,
                student_count INTEGER NOT NULL DEFAULT 0,
                UNIQUE(grade, letter)
            );

            CREATE TABLE IF NOT EXISTS rooms (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                number TEXT NOT NULL,
                building_id INTEGER NOT NULL REFERENCES buildings(id),
                capacity INTEGER NOT NULL DEFAULT 30,
                room_kind TEXT NOT NULL DEFAULT 'Regular',
                assigned_teacher_id INTEGER,
                UNIQUE(number, building_id)
            );

            CREATE TABLE IF NOT EXISTS teachers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                full_name TEXT NOT NULL UNIQUE,
                teacher_type TEXT NOT NULL DEFAULT 'Subject',
                max_load_hours INTEGER NOT NULL DEFAULT 18,
                room_id INTEGER REFERENCES rooms(id)
            );

            CREATE TABLE IF NOT EXISTS teacher_subjects (
                teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                subject_id INTEGER NOT NULL REFERENCES subjects(id),
                profile_type TEXT NOT NULL DEFAULT 'Primary',
                PRIMARY KEY (teacher_id, subject_id, profile_type)
            );

            CREATE TABLE IF NOT EXISTS class_teachers (
                teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                class_id INTEGER NOT NULL REFERENCES school_classes(id),
                PRIMARY KEY (teacher_id, class_id)
            );

            CREATE TABLE IF NOT EXISTS curriculum (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                class_id INTEGER NOT NULL REFERENCES school_classes(id),
                subject_id INTEGER NOT NULL REFERENCES subjects(id),
                hours_per_week REAL NOT NULL,
                has_subgroups INTEGER NOT NULL DEFAULT 0,
                UNIQUE(class_id, subject_id)
            );

            CREATE TABLE IF NOT EXISTS bell_templates (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS bell_periods (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id INTEGER NOT NULL REFERENCES bell_templates(id),
                lesson_number INTEGER NOT NULL,
                shift INTEGER NOT NULL DEFAULT 1,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                period_kind TEXT NOT NULL DEFAULT 'Lesson',
                UNIQUE(template_id, lesson_number, shift, period_kind)
            );

            CREATE TABLE IF NOT EXISTS week_templates (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                copied_from_id INTEGER REFERENCES week_templates(id)
            );

            CREATE TABLE IF NOT EXISTS schedule_periods (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                period_type TEXT NOT NULL,
                start_date TEXT NOT NULL,
                end_date TEXT NOT NULL,
                recurrence_cycle TEXT NOT NULL DEFAULT 'EveryWeek'
            );

            CREATE TABLE IF NOT EXISTS week_template_slots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                week_template_id INTEGER NOT NULL REFERENCES week_templates(id),
                day_of_week INTEGER NOT NULL,
                lesson_number INTEGER NOT NULL,
                class_id INTEGER NOT NULL REFERENCES school_classes(id),
                subject_id INTEGER NOT NULL REFERENCES subjects(id),
                teacher_id INTEGER NOT NULL REFERENCES teachers(id),
                room_id INTEGER NOT NULL REFERENCES rooms(id),
                subgroup_index INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS calendar_exceptions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                start_date TEXT NOT NULL,
                end_date TEXT,
                exception_type TEXT NOT NULL,
                donor_day_of_week INTEGER,
                week_template_id INTEGER REFERENCES week_templates(id),
                note TEXT
            );

            CREATE TABLE IF NOT EXISTS day_overrides (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL,
                override_type TEXT NOT NULL,
                class_id INTEGER REFERENCES school_classes(id),
                lesson_number INTEGER,
                teacher_id INTEGER REFERENCES teachers(id),
                replacement_teacher_id INTEGER REFERENCES teachers(id),
                room_id INTEGER REFERENCES rooms(id),
                bell_template_id INTEGER REFERENCES bell_templates(id),
                note TEXT
            );

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
