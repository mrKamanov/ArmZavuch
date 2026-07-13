namespace ArmZavuch.Models;

public static class ConstructorWorkflowSteps
{
    public const string Teachers = "Teachers";
    public const string Subjects = "Subjects";
    public const string Rooms = "Rooms";

    public static string ToDisplay(string step) => step switch
    {
        Teachers => "1 · Педагоги",
        Subjects => "2 · Предметы",
        Rooms => "3 · Кабинеты",
        _ => step
    };
}

public static class ConstructorGridLayout
{
    public const double ColumnWidth = 96;
    public const double RowHeaderWidth = 88;
    public const double CellMinHeight = 52;
    public const double BreakCellMinHeight = 12;
}
