using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArmZavuch.Views.Controls;

/// <summary>
/// Прокрутка ScrollViewer во время drag-and-drop: колёсико, Ctrl+масштаб, автоскролл у краёв.
/// Вход: зарегистрированные ScrollViewer. Выход: смещение сетки при перетаскивании карточки.
/// </summary>
public sealed class DragScrollCoordinator
{
    public static DragScrollCoordinator Instance { get; } = new();

    private const double EdgeThreshold = 52;
    private const double MaxScrollStep = 18;
    private const int MinScrollIntervalMs = 12;
    private const int WmMouseWheel = 0x020A;
    private const int WhMouseLl = 14;
    private const int VkControl = 0x11;

    private readonly List<ScrollViewer> _targets = [];
    private FrameworkElement? _dragSource;
    private ScrollViewer? _gridZoomHost;
    private Action<int>? _onGridWheelZoom;
    private NativeInput.LowLevelMouseProc? _mouseHookProc;
    private IntPtr _mouseHook;
    private bool _active;
    private long _lastScrollTicks;

    public void Register(ScrollViewer viewer)
    {
        if (!_targets.Contains(viewer))
            _targets.Add(viewer);
    }

    public void RegisterGridZoom(ScrollViewer gridHost, Action<int> onWheelDelta)
    {
        _gridZoomHost = gridHost;
        _onGridWheelZoom = onWheelDelta;
        if (!_targets.Contains(gridHost))
            _targets.Add(gridHost);
    }

    public void Begin(FrameworkElement? dragSource)
    {
        if (_active || dragSource is null)
            return;

        _active = true;
        _lastScrollTicks = 0;
        _dragSource = dragSource;
        _dragSource.QueryContinueDrag += OnQueryContinueDrag;
        _dragSource.GiveFeedback += OnGiveFeedback;
        InstallMouseHook();
    }

    public void End()
    {
        if (!_active)
            return;

        _active = false;
        UninstallMouseHook();

        if (_dragSource is not null)
        {
            _dragSource.QueryContinueDrag -= OnQueryContinueDrag;
            _dragSource.GiveFeedback -= OnGiveFeedback;
            _dragSource = null;
        }
    }

    public void ApplyDragOverScroll(ScrollViewer viewer, Point localPoint)
    {
        if (!_active)
            return;

        ApplyEdgeScroll(viewer, localPoint);
    }

    public void ApplyDragOverScrollFromEvent(DependencyObject? eventSource, DragEventArgs e)
    {
        if (!_active || eventSource is null)
            return;

        var viewer = FindContainingTarget(eventSource);
        if (viewer is null)
            return;

        ApplyDragOverScroll(viewer, e.GetPosition(viewer));
    }

    private ScrollViewer? FindContainingTarget(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is ScrollViewer viewer && _targets.Contains(viewer))
                return viewer;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void InstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
            return;

        _mouseHookProc = OnLowLevelMouse;
        _mouseHook = NativeInput.SetWindowsHookEx(
            WhMouseLl, _mouseHookProc, NativeInput.GetModuleHandle(null), 0);
    }

    private void UninstallMouseHook()
    {
        if (_mouseHook == IntPtr.Zero)
            return;

        NativeInput.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
        _mouseHookProc = null;
    }

    private IntPtr OnLowLevelMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _active && wParam == (IntPtr)WmMouseWheel)
            HandleWheelMessage(lParam);

        return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void HandleWheelMessage(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<NativeInput.MouseHookData>(lParam);
        var screen = new Point(data.Point.X, data.Point.Y);
        var delta = (short)((data.MouseData >> 16) & 0xFFFF);
        if (delta == 0)
            return;

        var ctrlPressed = (NativeInput.GetAsyncKeyState(VkControl) & 0x8000) != 0;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        if (ctrlPressed && _gridZoomHost is not null && _onGridWheelZoom is not null
            && TryMapScreenToViewer(_gridZoomHost, screen, out _))
        {
            dispatcher.BeginInvoke(() => _onGridWheelZoom(delta), DispatcherPriority.Input);
            return;
        }

        foreach (var viewer in _targets)
        {
            if (!TryMapScreenToViewer(viewer, screen, out _))
                continue;

            dispatcher.BeginInvoke(() =>
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset - delta), DispatcherPriority.Input);
            return;
        }
    }

    private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = true;
        ScrollAtCursor();
    }

    private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed)
            return;

        ScrollAtCursor();
    }

    private void ScrollAtCursor()
    {
        var now = Environment.TickCount64;
        if (now - _lastScrollTicks < MinScrollIntervalMs)
            return;

        if (!NativeInput.TryGetCursorScreenPoint(out var screen))
            return;

        var scrolled = false;
        foreach (var viewer in _targets)
        {
            if (!TryMapScreenToViewer(viewer, screen, out var local))
                continue;

            if (ApplyEdgeScroll(viewer, local))
                scrolled = true;
        }

        if (scrolled)
            _lastScrollTicks = now;
    }

    private static bool ApplyEdgeScroll(ScrollViewer viewer, Point local)
    {
        if (viewer.ActualWidth <= 0 || viewer.ActualHeight <= 0)
            return false;

        var vertical = EdgeSpeed(local.Y, viewer.ActualHeight);
        if (vertical != 0 && viewer.ScrollableHeight > 0)
        {
            viewer.ScrollToVerticalOffset(Clamp(
                viewer.VerticalOffset + vertical, 0, viewer.ScrollableHeight));
        }

        var horizontal = EdgeSpeed(local.X, viewer.ActualWidth);
        if (horizontal != 0 && viewer.ScrollableWidth > 0)
        {
            viewer.ScrollToHorizontalOffset(Clamp(
                viewer.HorizontalOffset + horizontal, 0, viewer.ScrollableWidth));
        }

        return vertical != 0 || horizontal != 0;
    }

    private static bool TryMapScreenToViewer(ScrollViewer viewer, Point screen, out Point local)
    {
        local = default;
        if (!viewer.IsVisible || viewer.ActualWidth <= 0 || viewer.ActualHeight <= 0)
            return false;

        try
        {
            local = viewer.PointFromScreen(screen);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return local.X >= 0 && local.Y >= 0
               && local.X <= viewer.ActualWidth && local.Y <= viewer.ActualHeight;
    }

    private static double EdgeSpeed(double coordinate, double size)
    {
        if (coordinate < EdgeThreshold)
            return -Step(EdgeThreshold - coordinate);

        if (coordinate > size - EdgeThreshold)
            return Step(coordinate - (size - EdgeThreshold));

        return 0;
    }

    private static double Step(double distanceIntoEdge) =>
        Math.Clamp(distanceIntoEdge / EdgeThreshold * MaxScrollStep, 3, MaxScrollStep);

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    private static class NativeInput
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct PointNative
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseHookData
        {
            public PointNative Point;
            public int MouseData;
            public int Flags;
            public int Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out PointNative point);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        public static bool TryGetCursorScreenPoint(out Point screen)
        {
            screen = default;
            if (!GetCursorPos(out var native))
                return false;

            screen = new Point(native.X, native.Y);
            return true;
        }
    }
}
