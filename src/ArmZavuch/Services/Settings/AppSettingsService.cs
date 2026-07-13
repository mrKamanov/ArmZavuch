using ArmZavuch.Data.Repositories;
using ArmZavuch.Services.Save;

namespace ArmZavuch.Services.Settings;

/// <summary>Настройки школы: название для экспорта и заголовков.</summary>
public sealed class AppSettingsService
{
    public const string SchoolNameKey = "school_name";
    private readonly AppSettingsRepository _repo;
    private readonly ISaveStateService _saveState;

    public string SchoolName { get; private set; } = "Школа";
    public event Action? SchoolNameChanged;

    public AppSettingsService(AppSettingsRepository repo, ISaveStateService saveState)
    {
        _repo = repo;
        _saveState = saveState;
    }

    public async Task LoadAsync()
    {
        var name = await _repo.GetAsync(SchoolNameKey);
        if (!string.IsNullOrWhiteSpace(name))
            SchoolName = name.Trim();
    }

    public async Task SaveSchoolNameAsync(string name)
    {
        SchoolName = string.IsNullOrWhiteSpace(name) ? "Школа" : name.Trim();
        await _repo.SetAsync(SchoolNameKey, SchoolName);
        _saveState.MarkDirty();
        SchoolNameChanged?.Invoke();
    }
}
