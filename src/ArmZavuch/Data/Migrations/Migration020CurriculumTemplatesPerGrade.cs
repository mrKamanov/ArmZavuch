using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Шаблоны нагрузки по отдельным параллелям (2–4 объединённый → 2, 3, 4; добавлены 5–6).</summary>
public sealed class Migration020CurriculumTemplatesPerGrade : IMigration
{
    public int Version => 20;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await CurriculumTemplateSeed.EnsureBuiltInAsync(conn);
    }
}
