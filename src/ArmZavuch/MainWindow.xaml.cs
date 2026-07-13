using System.Windows;
using ArmZavuch.Services.Shell;
using ArmZavuch.ViewModels;

namespace ArmZavuch;

/// <summary>
/// Главное окно: боковая навигация и область контента.
/// </summary>
public partial class MainWindow
{
    private readonly AppTrayService _tray;

    public MainWindow(MainViewModel viewModel, AppTrayService tray)
    {
        _tray = tray;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => _tray.Attach(this);
    }
}
