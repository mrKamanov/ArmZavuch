using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Документ листа замен для PNG и буфера обмена.</summary>
public sealed class SubstitutionExportDocument
{
    public required string SchoolName { get; init; }
    public required DateOnly Date { get; init; }
    public required IReadOnlyList<SubstitutionLine> Lines { get; init; }

    public int AssignedCount => Lines.Count(line => line.ExportKind == SubstitutionExportKind.Assigned);
    public int PendingCount => Lines.Count(line => line.ExportKind == SubstitutionExportKind.Pending);
    public int CancelledCount => Lines.Count(line => line.ExportKind == SubstitutionExportKind.Cancelled);
}
