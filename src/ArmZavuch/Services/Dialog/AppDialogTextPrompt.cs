namespace ArmZavuch.Services.Dialog;

/// <summary>Поле ввода в диалоге подтверждения.</summary>
public sealed class AppDialogTextPrompt
{
    public string Label { get; init; } = "Название";
    public string DefaultText { get; init; } = "";
}
