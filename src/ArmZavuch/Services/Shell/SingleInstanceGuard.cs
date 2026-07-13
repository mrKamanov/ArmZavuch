using System.Threading;
using System.Windows;

namespace ArmZavuch.Services.Shell;

/// <summary>
/// Гарантирует один запущенный экземпляр: повторный старт активирует уже открытое окно.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\ArmZavuch.SingleInstance";
    private const string ActivateEventName = @"Local\ArmZavuch.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly CancellationTokenSource _watchCts = new();
    private bool _ownsMutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activateEvent)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        _ownsMutex = true;
    }

    /// <summary>
    /// Вход: попытка занять слот единственного экземпляра.
    /// Выход: guard — первый экземпляр; null — уже запущено, сигнал активации отправлен.
    /// </summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                if (!mutex.WaitOne(0))
                {
                    SignalExistingInstance();
                    mutex.Dispose();
                    return null;
                }
            }
            catch (AbandonedMutexException)
            {
                // Предыдущий процесс завершился без освобождения — слот наш.
            }
        }

        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return new SingleInstanceGuard(mutex, activateEvent);
    }

    public void StartWatching(Action onActivate)
    {
        var token = _watchCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_activateEvent.WaitOne(500))
                        continue;

                    var app = Application.Current;
                    if (app is null)
                        continue;

                    app.Dispatcher.BeginInvoke(onActivate);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _watchCts.Cancel();
        _watchCts.Dispose();
        _activateEvent.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
            activate.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Окно ещё не создало событие — пользователь может запустить снова через мгновение.
        }
    }
}
