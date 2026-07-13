namespace ArmZavuch.Models;

/// <summary>Учебный класс (ТЗ §3): здание, кабинет по умолчанию, зал для физ-ры.</summary>
public sealed class SchoolClass : SelectableEntity
{
    public int Id { get; set; }
    public int Grade { get; set; }
    public string Letter { get; set; } = "";
    public int Shift { get; set; } = 1;
    public int StudentCount { get; set; }
    public bool IsCorrectional { get; set; }
    /// <summary>Корпус, где учится класс (для группировки в конструкторе).</summary>
    public int? BuildingId { get; set; }
    public string BuildingName { get; set; } = "";
    public string BuildingColorHex { get; set; } = "";
    public int? DefaultRoomId { get; set; }
    public string DefaultRoomDisplay { get; set; } = "";
    /// <summary>Зал/кабинет для физкультуры (может быть в другом здании).</summary>
    public int? DefaultPeRoomId { get; set; }
    public string DefaultPeRoomDisplay { get; set; } = "";
    public int? BellTemplateId { get; set; }
    public string BellTemplateName { get; set; } = "";
    public string DisplayName => $"{Grade}{Letter}";

    public string BellTemplateDisplay => BellTemplateId is null
        ? "По умолчанию"
        : BellTemplateNaming.ToDisplay(BellTemplateName);

    public string ShiftDisplay => Shift == 2 ? "2 смена" : "1 смена";

    public string TypeDisplay => IsCorrectional ? "Коррекционный" : "Обычный";
}
