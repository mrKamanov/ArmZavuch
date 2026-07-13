namespace ArmZavuch.Models;

/// <summary>Доступный релиз на GitHub.</summary>
public sealed class AvailableUpdate
{
    public required string Version { get; init; }
    public required string ReleaseNotes { get; init; }
    public required string ReleasePageUrl { get; init; }
    public string? SetupDownloadUrl { get; init; }
    public bool CanAutoInstall { get; init; }
    public object? VelopackUpdate { get; init; }
}
