using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArmZavuch.Views.Controls;

/// <summary>Контент вкладки с вертикальной прокруткой — для форм и длинных настроек.</summary>
public class ScrollableTabContent : ContentControl
{
    private ScrollViewer? _scrollViewer;

    public ScrollableTabContent()
    {
        if (Application.Current?.TryFindResource("ScrollableTabContentStyle") is Style style)
            Style = style;

        Loaded += (_, _) => _scrollViewer = FindTemplateScrollViewer(this);
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scrollViewer is null || _scrollViewer.ScrollableHeight <= 0)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        var innerScroll = FindInnerScrollViewer(source, _scrollViewer);
        if (innerScroll is not null && CanInnerScroll(innerScroll, e.Delta))
            return;

        _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static bool CanInnerScroll(ScrollViewer viewer, int delta)
    {
        if (viewer.ScrollableHeight <= 0)
            return false;

        if (delta > 0)
            return viewer.VerticalOffset > 0;

        return viewer.VerticalOffset < viewer.ScrollableHeight;
    }

    private static ScrollViewer? FindTemplateScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;

            var nested = FindTemplateScrollViewer(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static ScrollViewer? FindInnerScrollViewer(DependencyObject source, ScrollViewer outer)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ScrollViewer viewer && !ReferenceEquals(viewer, outer))
                return viewer;

            current = GetParentObject(current);
        }

        return null;
    }

    /// <summary>Run и другие inline-элементы не Visual — для них нельзя вызывать VisualTreeHelper.GetParent.</summary>
    private static DependencyObject? GetParentObject(DependencyObject current) =>
        current switch
        {
            Visual => VisualTreeHelper.GetParent(current),
            FrameworkContentElement fce => fce.Parent,
            _ => LogicalTreeHelper.GetParent(current)
        };
}
