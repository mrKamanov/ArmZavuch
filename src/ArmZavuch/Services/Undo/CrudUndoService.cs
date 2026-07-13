namespace ArmZavuch.Services.Undo;

/// <summary>Стек отмены последних CRUD-операций в справочниках и конструкторе.</summary>
public sealed class CrudUndoService
{
    private readonly Stack<Func<Task>> _stack = new();

    public bool CanUndo => _stack.Count > 0;

    public event Action? Changed;

    public void Push(Func<Task> undoAction)
    {
        _stack.Push(undoAction);
        Changed?.Invoke();
    }

    public async Task UndoAsync()
    {
        if (_stack.Count == 0)
            return;
        var action = _stack.Pop();
        Changed?.Invoke();
        await action();
    }

    public void Clear()
    {
        if (_stack.Count == 0)
            return;

        _stack.Clear();
        Changed?.Invoke();
    }
}
