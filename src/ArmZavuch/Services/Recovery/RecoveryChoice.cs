namespace ArmZavuch.Services.Recovery;

/// <summary>
/// Решение пользователя при обнаружении черновика восстановления.
/// </summary>
public enum RecoveryChoice
{
    NoDraft,
    UseSaved,
    RestoreDraft,
    DeleteDraft,
    ExitApp
}
