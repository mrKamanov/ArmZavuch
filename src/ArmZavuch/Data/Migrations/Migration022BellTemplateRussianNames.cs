using Microsoft.Data.Sqlite;

namespace ArmZavuch.Data.Migrations;

/// <summary>Русские имена шаблонов звонков: 1 класс, Начальная (2–5), Стандарт (5–11), 2 смена (5–11).</summary>
public sealed class Migration022BellTemplateRussianNames : IMigration
{
    public int Version => 22;

    public async Task ApplyAsync(SqliteConnectionFactory factory)
    {
        await using var conn = factory.CreateConnection();
        await BellTemplateConsolidation.ApplyAsync(conn);
    }
}
