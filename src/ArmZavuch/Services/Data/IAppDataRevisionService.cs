namespace ArmZavuch.Services.Data;

/// <summary>
/// Счётчики версий данных для ленивого обновления модулей при переключении вкладок.
/// Вход: сигналы мутаций. Выход: revision-номера для сравнения в ActivateAsync.
/// </summary>
public interface IAppDataRevisionService
{
    long ReferenceDataRevision { get; }

    long ScheduleRevision { get; }

    void NotifyReferenceDataChanged();

    void NotifyScheduleChanged();

    void NotifyAllDataChanged();
}
