namespace ArmZavuch.Models;

/// <summary>Описание архива выгрузки данных (.armzavuch).</summary>
public sealed class AppDataTransferManifest
{
    public string Format { get; set; } = "armzavuch-backup";
    public int FormatVersion { get; set; } = 2;
    public int SchemaVersion { get; set; }
    public string AppVersion { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public string SchoolName { get; set; } = "";
    /// <summary>Разделы, включённые в выгрузку. Пусто — полный архив (все разделы).</summary>
    public List<string> ExportedSections { get; set; } = [];

    public bool IsFullBackup =>
        ExportedSections.Count == 0
        || ExportedSections.Count >= Enum.GetValues<AppDataTransferSection>().Length;

    public IReadOnlyList<AppDataTransferSection> ResolveExportedSections()
    {
        if (ExportedSections.Count == 0)
            return Enum.GetValues<AppDataTransferSection>().Cast<AppDataTransferSection>().ToList();

        var parsed = new List<AppDataTransferSection>();
        foreach (var name in ExportedSections)
        {
            if (Enum.TryParse<AppDataTransferSection>(name, ignoreCase: true, out var section))
                parsed.Add(section);
        }

        return parsed.Count > 0
            ? parsed
            : Enum.GetValues<AppDataTransferSection>().Cast<AppDataTransferSection>().ToList();
    }
}
