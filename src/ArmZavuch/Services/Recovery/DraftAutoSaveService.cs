using ArmZavuch.Services.Save;
using Microsoft.Extensions.Hosting;

namespace ArmZavuch.Services.Recovery;

/// <summary>
/// Каждые 60 сек записывает черновик recovery.db, если есть несохранённые изменения.
/// </summary>
public sealed class DraftAutoSaveService : BackgroundService
{
    private readonly IRecoveryService _recovery;
    private readonly ISaveStateService _saveState;

    public DraftAutoSaveService(IRecoveryService recovery, ISaveStateService saveState)
    {
        _recovery = recovery;
        _saveState = saveState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

            if (_saveState.IsDirty)
                await _recovery.WriteDraftAsync();
        }
    }
}
