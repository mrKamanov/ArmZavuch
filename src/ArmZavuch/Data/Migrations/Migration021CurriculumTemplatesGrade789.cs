using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Шаблоны 7–9 классов и актуальные баллы Сивкова во встроенных шаблонах и subjects.</summary>
public sealed class Migration021CurriculumTemplatesGrade789 : IMigration
{
    public int Version => 21;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await CurriculumTemplateSeed.RefreshBuiltInAsync(conn);
    }
}
