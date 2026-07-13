namespace ArmZavuch.Services.Dialog;

public sealed class DialogAction
{
    public required string Text { get; init; }
    public AppDialogResult Result { get; init; }
    public DialogActionStyle Style { get; init; } = DialogActionStyle.Secondary;
    public bool IsDefault { get; init; }
}
