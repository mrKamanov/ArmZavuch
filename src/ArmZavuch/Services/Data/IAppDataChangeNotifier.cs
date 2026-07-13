namespace ArmZavuch.Services.Data;

/// <summary>Сигнал об изменении данных в БД (очистка, импорт) для сброса кэшей модулей.</summary>
public interface IAppDataChangeNotifier
{
    event EventHandler? DataChanged;

    void NotifyDataChanged();
}
