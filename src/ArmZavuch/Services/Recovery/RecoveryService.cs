using ArmZavuch.Data;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Save;
using Microsoft.Data.Sqlite;

namespace ArmZavuch.Services.Recovery;

/// <summary>
/// Копирует school.db в recovery.db в фоне. При старте сравнивает даты черновика и сохранения.
/// </summary>
public sealed class RecoveryService : IRecoveryService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ISaveStateService _saveState;
    private readonly IAppDialogService _dialogs;

    public RecoveryService(SqliteConnectionFactory factory, ISaveStateService saveState, IAppDialogService dialogs)
    {
        _factory = factory;
        _saveState = saveState;
        _dialogs = dialogs;
    }

    private string RecoveryPath => Path.Combine(
        Path.GetDirectoryName(_factory.DatabasePath)!,
        "recovery.db");

    public Task<RecoveryChoice> CheckOnStartupAsync()
    {
        if (!File.Exists(RecoveryPath))
            return Task.FromResult(RecoveryChoice.NoDraft);

        var draftTime = File.GetLastWriteTime(RecoveryPath);
        var savedTime = _saveState.LastSavedAt ?? DateTime.MinValue;

        if (draftTime <= savedTime)
            return Task.FromResult(RecoveryChoice.NoDraft);

        return Task.FromResult(_dialogs.AskRecoveryChoice(draftTime, _saveState.LastSavedAt));
    }

    public Task RestoreDraftAsync()
    {
        RecoveryDatabaseHelper.ReplaceDatabase(_factory.DatabasePath, RecoveryPath);
        return Task.CompletedTask;
    }

    public Task DiscardDraftAsync()
    {
        RecoveryDatabaseHelper.DeleteDatabaseFiles(RecoveryPath);
        return Task.CompletedTask;
    }

    public Task WriteDraftAsync()
    {
        if (!File.Exists(_factory.DatabasePath))
            return Task.CompletedTask;

        SqliteConnection.ClearAllPools();
        File.Copy(_factory.DatabasePath, RecoveryPath, overwrite: true);
        return Task.CompletedTask;
    }
}
