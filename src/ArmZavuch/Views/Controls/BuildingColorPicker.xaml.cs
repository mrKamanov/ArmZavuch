using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArmZavuch.Models;

namespace ArmZavuch.Views.Controls;

public partial class BuildingColorPicker
{
    public static readonly DependencyProperty SelectedColorHexProperty =
        DependencyProperty.Register(
            nameof(SelectedColorHex),
            typeof(string),
            typeof(BuildingColorPicker),
            new FrameworkPropertyMetadata(BuildingColors.DefaultHex, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ColorChoicesProperty =
        DependencyProperty.Register(
            nameof(ColorChoices),
            typeof(IEnumerable),
            typeof(BuildingColorPicker),
            new PropertyMetadata(BuildingColors.Palette));

    public static readonly DependencyProperty PickColorCommandProperty =
        DependencyProperty.Register(nameof(PickColorCommand), typeof(ICommand), typeof(BuildingColorPicker));

    public static readonly DependencyProperty BlockedColorHexesProperty =
        DependencyProperty.Register(
            nameof(BlockedColorHexes),
            typeof(IEnumerable),
            typeof(BuildingColorPicker),
            new PropertyMetadata(Array.Empty<string>()));

    public string SelectedColorHex
    {
        get => (string)GetValue(SelectedColorHexProperty);
        set => SetValue(SelectedColorHexProperty, value);
    }

    public IEnumerable ColorChoices
    {
        get => (IEnumerable)GetValue(ColorChoicesProperty);
        set => SetValue(ColorChoicesProperty, value);
    }

    public ICommand? PickColorCommand
    {
        get => (ICommand?)GetValue(PickColorCommandProperty);
        set => SetValue(PickColorCommandProperty, value);
    }

    public IEnumerable BlockedColorHexes
    {
        get => (IEnumerable)GetValue(BlockedColorHexesProperty);
        set => SetValue(BlockedColorHexesProperty, value);
    }

    public BuildingColorPicker()
    {
        InitializeComponent();
    }
}
