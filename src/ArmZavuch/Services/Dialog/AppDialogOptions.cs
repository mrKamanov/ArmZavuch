using System.Windows;

namespace ArmZavuch.Services.Dialog;

public sealed class AppDialogOptions
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public AppDialogKind Kind { get; init; } = AppDialogKind.Info;
    public required IReadOnlyList<DialogAction> Actions { get; init; }
    public Window? Owner { get; init; }
    public bool ShowCloseButton { get; init; } = true;
    public string? CloseTooltip { get; init; }
    public DialogButtonLayout ButtonLayout { get; init; } = DialogButtonLayout.Horizontal;
    public AppDialogTextPrompt? TextPrompt { get; init; }
}
