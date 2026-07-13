namespace ArmZavuch.ViewModels;

/// <summary>После перезагрузки списка: новая запись — чистая форма, правка — восстановить выбор по id.</summary>
internal static class CrudFormHelper
{
    public static void ApplyAfterReload<T>(
        bool isNew,
        int? savedId,
        IEnumerable<T> items,
        Func<T, int> getId,
        Action beginNew,
        Action<T?> setSelected)
    {
        if (isNew)
        {
            beginNew();
            return;
        }

        if (savedId is int id)
            setSelected(items.FirstOrDefault(x => getId(x) == id));
    }
}
