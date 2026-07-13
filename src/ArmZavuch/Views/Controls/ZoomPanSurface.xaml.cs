using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace ArmZavuch.Views.Controls;

/// <summary>Полотно с масштабом (колёсико) и перемещением (Shift+ЛКМ или средняя кнопка).</summary>
[ContentProperty(nameof(ZoomContent))]
public partial class ZoomPanSurface : UserControl
{
    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZoomPanSurface),
            new PropertyMetadata(1.0, OnZoomPropertyChanged));

    public static readonly DependencyProperty ZoomContentProperty =
        DependencyProperty.Register(nameof(ZoomContent), typeof(object), typeof(ZoomPanSurface),
            new PropertyMetadata(null, OnZoomContentChanged));

    private Point _panAnchor;
    private Point _translateAnchor;
    private bool _isPanning;

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public object? ZoomContent
    {
        get => GetValue(ZoomContentProperty);
        set => SetValue(ZoomContentProperty, value);
    }

    public ZoomPanSurface()
    {
        InitializeComponent();
        ApplyTransform();
        DataContextChanged += (_, _) => SyncZoomContentContext();
    }

    private static void OnZoomContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomPanSurface surface)
            surface.SyncZoomContentContext();
    }

    public void SyncZoomContentContext()
    {
        if (ZoomContent is not FrameworkElement content)
            return;

        var context = ResolveDataContext();
        if (context is not null && !ReferenceEquals(content.DataContext, context))
            content.DataContext = context;
    }

    private object? ResolveDataContext()
    {
        if (DataContext is not null)
            return DataContext;

        var parent = Parent as FrameworkElement;
        while (parent is not null)
        {
            if (parent.DataContext is not null)
                return parent.DataContext;
            parent = parent.Parent as FrameworkElement;
        }

        return null;
    }

    public void ResetView()
    {
        Zoom = 1.0;
        TranslateTransform.X = 0;
        TranslateTransform.Y = 0;
    }

    private static void OnZoomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomPanSurface surface)
            surface.ApplyTransform();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        ContentHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = double.IsInfinity(constraint.Width)
            ? Math.Max(ContentHost.DesiredSize.Width, 320)
            : constraint.Width;
        var height = double.IsInfinity(constraint.Height)
            ? Math.Max(ContentHost.DesiredSize.Height, 240)
            : constraint.Height;

        Viewport.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        Viewport.Arrange(new Rect(0, 0, arrangeBounds.Width, arrangeBounds.Height));
        return arrangeBounds;
    }

    private void ApplyTransform()
    {
        var zoom = Math.Clamp(Zoom, 0.2, 4.0);
        ScaleTransform.ScaleX = zoom;
        ScaleTransform.ScaleY = zoom;
    }

    private void Viewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Viewport.IsMouseOver)
            return;

        ZoomAtPoint(e.GetPosition(Viewport), e.Delta > 0 ? 1.12 : 1 / 1.12);
        e.Handled = true;
    }

    private void ZoomAtPoint(Point pivot, double factor)
    {
        var oldZoom = Math.Clamp(Zoom, 0.2, 4.0);
        var newZoom = Math.Clamp(oldZoom * factor, 0.2, 4.0);
        if (Math.Abs(newZoom - oldZoom) < 0.0001)
            return;

        var ratio = newZoom / oldZoom;
        TranslateTransform.X = pivot.X - ratio * (pivot.X - TranslateTransform.X);
        TranslateTransform.Y = pivot.Y - ratio * (pivot.Y - TranslateTransform.Y);
        Zoom = newZoom;
    }

    private void Viewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var panButton = e.ChangedButton == MouseButton.Middle
                        || (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Shift);
        if (!panButton)
            return;

        _isPanning = true;
        _panAnchor = e.GetPosition(Viewport);
        _translateAnchor = new Point(TranslateTransform.X, TranslateTransform.Y);
        Viewport.CaptureMouse();
        Viewport.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
            return;

        var pos = e.GetPosition(Viewport);
        TranslateTransform.X = _translateAnchor.X + (pos.X - _panAnchor.X);
        TranslateTransform.Y = _translateAnchor.Y + (pos.Y - _panAnchor.Y);
        e.Handled = true;
    }

    private void Viewport_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
            return;
        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Left)
            return;

        _isPanning = false;
        Viewport.ReleaseMouseCapture();
        Viewport.Cursor = Cursors.Arrow;
        e.Handled = true;
    }
}
