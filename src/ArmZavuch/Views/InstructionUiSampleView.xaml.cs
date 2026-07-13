using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArmZavuch.Services.Help;

namespace ArmZavuch.Views;

/// <summary>Рендерит наглядный образец элемента интерфейса в статье справки.</summary>
public partial class InstructionUiSampleView : UserControl
{
    public static readonly DependencyProperty SampleKindProperty =
        DependencyProperty.Register(
            nameof(SampleKind),
            typeof(InstructionUiSampleKind),
            typeof(InstructionUiSampleView),
            new PropertyMetadata(InstructionUiSampleKind.PrimaryButton, OnSampleChanged));

    public static readonly DependencyProperty SampleTextProperty =
        DependencyProperty.Register(
            nameof(SampleText),
            typeof(string),
            typeof(InstructionUiSampleView),
            new PropertyMetadata(null, OnSampleChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(
            nameof(Caption),
            typeof(string),
            typeof(InstructionUiSampleView),
            new PropertyMetadata(null, OnCaptionChanged));

    public InstructionUiSampleKind SampleKind
    {
        get => (InstructionUiSampleKind)GetValue(SampleKindProperty);
        set => SetValue(SampleKindProperty, value);
    }

    public string? SampleText
    {
        get => (string?)GetValue(SampleTextProperty);
        set => SetValue(SampleTextProperty, value);
    }

    public string? Caption
    {
        get => (string?)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public InstructionUiSampleView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySample();
    }

    private static void OnSampleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InstructionUiSampleView)d).ApplySample();

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InstructionUiSampleView)d).UpdateCaption();

    private void ApplySample()
    {
        HideAll();
        switch (SampleKind)
        {
            case InstructionUiSampleKind.PrimaryButton:
                ShowButton(PrimaryButton, SampleText ?? "Сохранить");
                break;
            case InstructionUiSampleKind.SecondaryButton:
                ShowButton(SecondaryButton, SampleText ?? "Отмена");
                break;
            case InstructionUiSampleKind.DangerButton:
                ShowButton(DangerButton, SampleText ?? "Очистить всё");
                break;
            case InstructionUiSampleKind.SaveUnsaved:
                ShowSave(SampleText ?? "●", new SolidColorBrush(Color.FromRgb(245, 158, 11)));
                break;
            case InstructionUiSampleKind.SaveSaved:
                ShowSave(SampleText ?? "✓", new SolidColorBrush(Color.FromRgb(34, 197, 94)));
                break;
            case InstructionUiSampleKind.GridCellConflict:
                ShowGridCell("#FEF2F2", "#DC2626", SampleText ?? "5«А» · мат.");
                break;
            case InstructionUiSampleKind.GridCellWarning:
                ShowGridCell("#FFFBEB", "#F59E0B", SampleText ?? "5«А» · физ-ра");
                break;
            case InstructionUiSampleKind.GridCellOk:
                ShowGridCell("#DCFCE7", "#16A34A", SampleText ?? "5«А» · впр");
                break;
            case InstructionUiSampleKind.LessonNeedsReplace:
                ShowLessonCell("#FEF2F2", "#FECACA", "5«А» · матем.", "Иванова — нужна замена");
                break;
            case InstructionUiSampleKind.LessonReplaced:
                ShowLessonCell("#F0FDF4", "#BBF7D0", "5«А» · матем.", "Замена: Петрова");
                break;
            case InstructionUiSampleKind.BadgeNoReplacement:
                ShowBadge(SampleText is null ? "Без замены: 3" : $"Без замены: {SampleText}");
                break;
            case InstructionUiSampleKind.DragOk:
                ShowDragHint(Color.FromRgb(34, 197, 94), SampleText ?? "Учитель свободен");
                break;
            case InstructionUiSampleKind.DragWarn:
                ShowDragHint(Color.FromRgb(202, 138, 4), SampleText ?? "Долгий переход между корпусами");
                break;
            case InstructionUiSampleKind.DragBlock:
                ShowDragHint(Color.FromRgb(220, 38, 38), SampleText ?? "Учитель уже занят");
                break;
            case InstructionUiSampleKind.BuildingStripe:
                BuildingStripePanel.Visibility = Visibility.Visible;
                break;
        }

        UpdateCaption();
    }

    private void HideAll()
    {
        PrimaryButton.Visibility = Visibility.Collapsed;
        SecondaryButton.Visibility = Visibility.Collapsed;
        DangerButton.Visibility = Visibility.Collapsed;
        SavePanel.Visibility = Visibility.Collapsed;
        GridCell.Visibility = Visibility.Collapsed;
        LessonCell.Visibility = Visibility.Collapsed;
        Badge.Visibility = Visibility.Collapsed;
        DragHint.Visibility = Visibility.Collapsed;
        BuildingStripePanel.Visibility = Visibility.Collapsed;
    }

    private static void ShowButton(Border border, string text)
    {
        ((TextBlock)border.Child).Text = text;
        border.Visibility = Visibility.Visible;
    }

    private void ShowSave(string glyph, Brush color)
    {
        SaveGlyph.Text = glyph;
        SaveGlyph.Foreground = color;
        SavePanel.Visibility = Visibility.Visible;
    }

    private void ShowGridCell(string background, string border, string text)
    {
        GridCell.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background)!);
        GridCell.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border)!);
        GridCellText.Text = text;
        GridCell.Visibility = Visibility.Visible;
    }

    private void ShowLessonCell(string background, string border, string title, string sub)
    {
        LessonCell.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background)!);
        LessonCell.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border)!);
        LessonTitle.Text = title;
        LessonSub.Text = sub;
        LessonCell.Visibility = Visibility.Visible;
    }

    private void ShowBadge(string text)
    {
        BadgeText.Text = text;
        Badge.Visibility = Visibility.Visible;
    }

    private void ShowDragHint(Color color, string text)
    {
        DragEllipseFill.Color = color;
        DragHintText.Text = text;
        DragHint.Visibility = Visibility.Visible;
    }

    private void UpdateCaption()
    {
        var hasCaption = !string.IsNullOrWhiteSpace(Caption);
        CaptionBlock.Text = Caption ?? "";
        CaptionBlock.Visibility = hasCaption ? Visibility.Visible : Visibility.Collapsed;
    }
}
