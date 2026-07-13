using System.Collections.ObjectModel;

namespace ArmZavuch.Models;

/// <summary>Строка сводной простыни.</summary>
public sealed class OverviewMatrixRow
{
    public string RowLabel { get; init; } = "";
    public string? RowSubLabel { get; init; }
    public string RowKind { get; init; } = OverviewRowKinds.Class;
    public int? EntityId { get; init; }
    public string? BuildingName { get; init; }
    public string BuildingColorHex { get; init; } = "";
    public bool IsSectionHeader { get; init; }
    public string? SectionTitle { get; init; }
    public bool ShowDayHeadersBelow { get; init; }
    public bool IsDayHeaderRow { get; init; }
    public bool ShowsDayHeaders => IsDayHeaderRow || ShowDayHeadersBelow;
    public int? ClassShift { get; init; }
    public bool HasRowSeparatorBelow { get; init; }
    public bool IsTrackGroupEnd { get; init; }
    public ObservableCollection<OverviewDayColumn> Days { get; } = [];
}
