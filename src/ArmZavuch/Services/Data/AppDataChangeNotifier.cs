namespace ArmZavuch.Services.Data;

public sealed class AppDataChangeNotifier : IAppDataChangeNotifier
{
    private readonly IAppDataRevisionService _revision;

    public AppDataChangeNotifier(IAppDataRevisionService revision) => _revision = revision;

    public event EventHandler? DataChanged;

    public void NotifyDataChanged()
    {
        _revision.NotifyAllDataChanged();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
