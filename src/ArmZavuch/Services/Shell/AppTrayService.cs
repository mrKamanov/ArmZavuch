using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace ArmZavuch.Services.Shell;

/// <summary>Иконка в системном трее, сворачивание окна и контекстное меню.</summary>
public sealed class AppTrayService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;
    private bool _exitRequested;
    private bool _disposed;

    public void Attach(Window mainWindow)
    {
        _mainWindow = mainWindow;
        mainWindow.StateChanged += OnMainWindowStateChanged;
        mainWindow.Closing += OnMainWindowClosing;

        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/AppIcon.png", UriKind.Absolute)),
            ToolTipText = AppBranding.ProductName,
            Visibility = Visibility.Collapsed
        };
        _trayIcon.TrayMouseDoubleClick += (_, _) => Restore();
        _trayIcon.ContextMenu = BuildContextMenu();
    }

    public void MinimizeToTray()
    {
        if (_mainWindow is null || !_mainWindow.IsVisible)
            return;

        _mainWindow.Hide();
        _mainWindow.ShowInTaskbar = false;
        if (_trayIcon is not null)
            _trayIcon.Visibility = Visibility.Visible;
    }

    public void Restore()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        if (_trayIcon is not null)
            _trayIcon.Visibility = Visibility.Collapsed;
    }

    public void RequestExit()
    {
        _exitRequested = true;
        if (_trayIcon is not null)
            _trayIcon.Visibility = Visibility.Collapsed;
        if (_mainWindow is not null)
            _mainWindow.Close();
        else
            Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.FindResource("TrayContextMenuStyle")
        };
        menu.Resources[typeof(MenuItem)] = Application.Current.FindResource("TrayMenuItemStyle");
        menu.Resources[typeof(Separator)] = Application.Current.FindResource("TrayMenuSeparatorStyle");

        var header = new MenuItem
        {
            Header = AppBranding.ProductName,
            Style = (Style)Application.Current.FindResource("TrayMenuHeaderItemStyle")
        };

        var open = new MenuItem { Header = "↗  Открыть окно" };
        open.Click += (_, _) => Restore();

        var exit = new MenuItem
        {
            Header = "✕  Выход",
            Style = (Style)Application.Current.FindResource("TrayMenuDangerItemStyle")
        };
        exit.Click += (_, _) => RequestExit();

        menu.Items.Add(header);
        menu.Items.Add(new Separator());
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        return menu;
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized)
            MinimizeToTray();
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
            return;

        e.Cancel = true;
        MinimizeToTray();
    }
}
