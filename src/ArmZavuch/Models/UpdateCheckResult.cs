namespace ArmZavuch.Models;

/// <summary>Результат проверки обновлений.</summary>
public abstract record UpdateCheckResult
{
    public sealed record UpToDate(string Message) : UpdateCheckResult;
    public sealed record Available(AvailableUpdate Update) : UpdateCheckResult;
    public sealed record Failed(string Message) : UpdateCheckResult;
    public sealed record Skipped(string Reason) : UpdateCheckResult;
}
