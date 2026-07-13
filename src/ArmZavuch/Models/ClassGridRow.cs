using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Строка сетки Конструктора — один класс, ячейки по урокам.</summary>
public sealed class ClassGridRow
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public ObservableCollection<GridCell> Lessons { get; } = [];
}
