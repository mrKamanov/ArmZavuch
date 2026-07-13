using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Services.Dialog;

namespace ArmZavuch.Views.Dialogs;

public partial class AppDialogWindow : Window
{
    private readonly IReadOnlyList<DialogAction> _actions;
    private readonly bool _showCloseButton;

    public AppDialogResult Result { get; private set; } = AppDialogResult.None;
    public string? EnteredText { get; private set; }

    public AppDialogWindow(AppDialogOptions options)
    {
        InitializeComponent();
        _actions = options.Actions;
        _showCloseButton = options.ShowCloseButton;
        TitleText.Text = options.Title;
        MessageText.Text = options.Message;
        ApplyKind(options.Kind);
        ApplyButtonLayout(options.ButtonLayout);
        ApplyTextPrompt(options.TextPrompt);
        CloseButton.Visibility = _showCloseButton ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.ToolTip = options.CloseTooltip ?? "Закрыть";

        foreach (var action in _actions)
            ButtonsPanel.Children.Add(CreateButton(action, options.ButtonLayout));

        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += (_, _) => AlignToOwner();
        Loaded += (_, _) =>
        {
            AlignToOwner();
            if (InputPanel.Visibility == Visibility.Visible)
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }
        };
    }

    private void ApplyTextPrompt(AppDialogTextPrompt? prompt)
    {
        if (prompt is null)
            return;

        InputPanel.Visibility = Visibility.Visible;
        InputLabelText.Text = prompt.Label;
        InputTextBox.Text = prompt.DefaultText;
    }

    private void AlignToOwner()
    {
        if (Owner is not Window owner)
            return;

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        Left = owner.Left + Math.Max(0, (owner.ActualWidth - ActualWidth) / 2);
        Top = owner.Top + Math.Max(0, (owner.ActualHeight - ActualHeight) / 2);
    }

    private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void ApplyButtonLayout(DialogButtonLayout layout)
    {
        if (layout != DialogButtonLayout.Vertical)
            return;

        ButtonsPanel.Orientation = Orientation.Vertical;
        ButtonsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void ApplyKind(AppDialogKind kind)
    {
        (string glyph, string bg, string fg) = kind switch
        {
            AppDialogKind.Warning => ("⚠", "#FEF3C7", "#B45309"),
            AppDialogKind.Error => ("✕", "#FEE2E2", "#DC2626"),
            AppDialogKind.Success => ("✓", "#DCFCE7", "#15803D"),
            AppDialogKind.Question => ("💾", "#EFF6FF", "#2563EB"),
            _ => ("ℹ", "#EFF6FF", "#2563EB")
        };

        IconText.Text = glyph;
        IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)!);
        IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)!);
    }

    private Button CreateButton(DialogAction action, DialogButtonLayout layout)
    {
        var styleKey = action.Style switch
        {
            DialogActionStyle.Primary => "PrimaryButtonStyle",
            DialogActionStyle.Danger => "DangerButtonStyle",
            _ => "SecondaryButtonStyle"
        };

        var isVertical = layout == DialogButtonLayout.Vertical;
        var button = new Button
        {
            Content = action.Text,
            Margin = isVertical ? new Thickness(0, 8, 0, 0) : new Thickness(8, 0, 0, 0),
            MinWidth = isVertical ? 0 : 96,
            HorizontalAlignment = isVertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
            IsDefault = action.IsDefault,
            Style = (Style)FindResource(styleKey)
        };

        button.Click += (_, _) => CloseWith(action.Result);

        return button;
    }

    private void CloseWith(AppDialogResult result)
    {
        if (InputPanel.Visibility == Visibility.Visible && result == AppDialogResult.Yes)
            EnteredText = InputTextBox.Text;

        Result = result;
        DialogResult = result != AppDialogResult.None;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseWith(AppDialogResult.None);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (_actions.Any(a => a.Text == "Отмена"))
        {
            CloseWith(AppDialogResult.No);
            e.Handled = true;
            return;
        }

        CloseWith(AppDialogResult.None);
        e.Handled = true;
    }
}
