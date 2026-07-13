namespace ArmZavuch.Services.Recovery;

/// <summary>
/// Черновик recovery.db: автосохранение при работе, диалог при старте.
/// </summary>
public interface IRecoveryService
{
    Task<RecoveryChoice> CheckOnStartupAsync();
    Task RestoreDraftAsync();
    Task DiscardDraftAsync();
    Task WriteDraftAsync();
}
