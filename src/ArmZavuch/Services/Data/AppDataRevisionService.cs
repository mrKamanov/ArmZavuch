namespace ArmZavuch.Services.Data;

/// <summary>
/// In-memory счётчики версий справочников и расписания (без обращения к БД).
/// </summary>
public sealed class AppDataRevisionService : IAppDataRevisionService
{
    public long ReferenceDataRevision { get; private set; }

    public long ScheduleRevision { get; private set; }

    public void NotifyReferenceDataChanged() => ReferenceDataRevision++;

    public void NotifyScheduleChanged() => ScheduleRevision++;

    public void NotifyAllDataChanged()
    {
        ReferenceDataRevision++;
        ScheduleRevision++;
    }
}
