namespace ArmZavuch.Models;

public sealed class AppDataTransferResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public AppDataTransferManifest? Manifest { get; init; }

    public static AppDataTransferResult Ok(string message, AppDataTransferManifest? manifest = null) =>
        new() { Success = true, Message = message, Manifest = manifest };

    public static AppDataTransferResult Fail(string message) =>
        new() { Success = false, Message = message };
}
