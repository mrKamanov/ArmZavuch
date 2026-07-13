using System.Windows;
using System.Windows.Input;

namespace ArmZavuch.Views.Controls;

/// <summary>
/// Строка панели конструктора дня: подпись, степпер ±, единица, кнопка «Применить».
/// Вход: команды и значение из ViewModel. Выход: единая сетка с SharedSizeGroup.
/// </summary>
public partial class DayEditorStepperRow
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(DayEditorStepperRow), new PropertyMetadata("мин"));

    public static readonly DependencyProperty ApplyLabelProperty =
        DependencyProperty.Register(nameof(ApplyLabel), typeof(string), typeof(DayEditorStepperRow), new PropertyMetadata("Применить"));

    public static readonly DependencyProperty DecreaseCommandProperty =
        DependencyProperty.Register(nameof(DecreaseCommand), typeof(ICommand), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty IncreaseCommandProperty =
        DependencyProperty.Register(nameof(IncreaseCommand), typeof(ICommand), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty ApplyCommandProperty =
        DependencyProperty.Register(nameof(ApplyCommand), typeof(ICommand), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty DecreaseToolTipProperty =
        DependencyProperty.Register(nameof(DecreaseToolTip), typeof(string), typeof(DayEditorStepperRow));

    public static readonly DependencyProperty IncreaseToolTipProperty =
        DependencyProperty.Register(nameof(IncreaseToolTip), typeof(string), typeof(DayEditorStepperRow));

    public DayEditorStepperRow() => InitializeComponent();

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string ApplyLabel
    {
        get => (string)GetValue(ApplyLabelProperty);
        set => SetValue(ApplyLabelProperty, value);
    }

    public ICommand? DecreaseCommand
    {
        get => (ICommand?)GetValue(DecreaseCommandProperty);
        set => SetValue(DecreaseCommandProperty, value);
    }

    public ICommand? IncreaseCommand
    {
        get => (ICommand?)GetValue(IncreaseCommandProperty);
        set => SetValue(IncreaseCommandProperty, value);
    }

    public ICommand? ApplyCommand
    {
        get => (ICommand?)GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public string? DecreaseToolTip
    {
        get => (string?)GetValue(DecreaseToolTipProperty);
        set => SetValue(DecreaseToolTipProperty, value);
    }

    public string? IncreaseToolTip
    {
        get => (string?)GetValue(IncreaseToolTipProperty);
        set => SetValue(IncreaseToolTipProperty, value);
    }
}
