using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Models;

public sealed class SubjectPaletteClassFilter
{
    public SchoolClass? Class { get; init; }
    public string DisplayName { get; init; } = "";
}

public sealed class SubjectPaletteItem
{
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = "";
    public double DifficultyScore { get; init; }
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public double RemainingHours { get; init; }
    public bool ShowClassInName { get; init; }

    public SubjectAccentColors.Accent SubjectAccent => SubjectAccentColors.Resolve(SubjectName);
    public string AccentBorderHex => SubjectAccent.BorderHex;
    public string AccentBackgroundHex => SubjectAccent.BackgroundHex;
    public string SubjectBadgeText => SubjectAccent.BadgeText;

    public string PrimaryLine => ShowClassInName
        ? $"{SubjectName} {ClassName}"
        : SubjectName;
    public string SecondaryLine => $"Сивков {DifficultyScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} · осталось {RemainingHours:0.#} ч";
}

public sealed class RoomPaletteItem
{
    public required Room Room { get; init; }
    public string PrimaryLine => $"каб. {Room.Number}";
    public string SecondaryLine => Room.BuildingName;
    public string BuildingColorHex => Room.BuildingColorHex;
}

public sealed class WeeklyLoadChartPoint
{
    public int DayOfWeek { get; init; }
    public string DayName { get; init; } = "";
    public double TotalScore { get; init; }
    public bool IsPeakDay { get; init; }
    public double ChartX { get; set; }
    public double ChartY { get; set; }
    public double ValueLabelX { get; set; }
    public double ValueLabelY { get; set; }
    public bool IsValueLabelBelow { get; set; }
    public string ScoreLabel => TotalScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
