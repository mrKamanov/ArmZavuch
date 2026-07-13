namespace ArmZavuch.Services.Save;

/// <summary>
/// Отслеживает несохранённые изменения и явное сохранение в основной .db.
/// </summary>
public interface ISaveStateService
{
    bool IsDirty { get; }
    DateTime? LastSavedAt { get; }
    event Action? DirtyStateChanged;
    void MarkDirty();
    Task SaveAsync();
}
