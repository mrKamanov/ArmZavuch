using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ArmZavuch.Services.Schedule;

namespace ArmZavuch.Views.Controls;

public partial class WeeklyLoadChartSurface : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IEnumerable), typeof(WeeklyLoadChartSurface));

    public static readonly DependencyProperty PolylinePointsProperty =
        DependencyProperty.Register(nameof(PolylinePoints), typeof(string), typeof(WeeklyLoadChartSurface));

    public static readonly DependencyProperty PlotWidthProperty =
        DependencyProperty.Register(nameof(PlotWidth), typeof(double), typeof(WeeklyLoadChartSurface),
            new PropertyMetadata(260.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PlotHeightProperty =
        DependencyProperty.Register(nameof(PlotHeight), typeof(double), typeof(WeeklyLoadChartSurface),
            new PropertyMetadata(56.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsLargeProperty =
        DependencyProperty.Register(nameof(IsLarge), typeof(bool), typeof(WeeklyLoadChartSurface),
            new PropertyMetadata(false, OnLayoutPropertyChanged));

    public event PropertyChangedEventHandler? PropertyChanged;

    public IEnumerable? Points
    {
        get => (IEnumerable?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string PolylinePoints
    {
        get => (string)GetValue(PolylinePointsProperty);
        set => SetValue(PolylinePointsProperty, value);
    }

    public double PlotWidth
    {
        get => (double)GetValue(PlotWidthProperty);
        set => SetValue(PlotWidthProperty, value);
    }

    public double PlotHeight
    {
        get => (double)GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    public double PlotTopReserve => WeeklyLoadChartBuilder.GetTopLabelReserve(PlotHeight);
    public double PlotBottomMargin => WeeklyLoadChartBuilder.BottomMargin;
    public double PlotDataHeight => PlotHeight - PlotTopReserve - PlotBottomMargin;
    public double PlotCanvasHeight => PlotHeight + 36;
    public double PlotAreaHeight => PlotHeight;
    public double PlotAreaBottom => PlotHeight - PlotBottomMargin;
    public double PlotAreaMidHigh => PlotTopReserve + PlotDataHeight * 0.35;
    public double PlotAreaMidLow => PlotTopReserve + PlotDataHeight * 0.65;
    public double PlotLineRight => PlotWidth - 14;
    public double LineThickness => IsLarge ? 3.5 : 2.5;
    public double DotSize => IsLarge ? 10 : 7;
    public Thickness DotMargin => IsLarge ? new Thickness(-5, -5, 0, 0) : new Thickness(-3.5, -3.5, 0, 0);
    public double DayLabelWidth => IsLarge ? 40 : 32;
    public Thickness DayLabelMargin => IsLarge ? new Thickness(-20, 0, 0, 0) : new Thickness(-16, 0, 0, 0);

    public WeeklyLoadChartSurface()
    {
        InitializeComponent();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WeeklyLoadChartSurface surface)
            surface.NotifyLayout();
    }

    private void NotifyLayout()
    {
        OnPropertyChanged(nameof(PlotTopReserve));
        OnPropertyChanged(nameof(PlotBottomMargin));
        OnPropertyChanged(nameof(PlotDataHeight));
        OnPropertyChanged(nameof(PlotCanvasHeight));
        OnPropertyChanged(nameof(PlotAreaHeight));
        OnPropertyChanged(nameof(PlotAreaBottom));
        OnPropertyChanged(nameof(PlotAreaMidHigh));
        OnPropertyChanged(nameof(PlotAreaMidLow));
        OnPropertyChanged(nameof(PlotLineRight));
        OnPropertyChanged(nameof(LineThickness));
        OnPropertyChanged(nameof(DotSize));
        OnPropertyChanged(nameof(DotMargin));
        OnPropertyChanged(nameof(DayLabelWidth));
        OnPropertyChanged(nameof(DayLabelMargin));
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
