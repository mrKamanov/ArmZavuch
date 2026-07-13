using System.Windows;
using System.Windows.Input;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views;

public partial class OverviewView
{
    public OverviewView(OverviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.ActivateAsync();
    }

    private OverviewViewModel Vm => (OverviewViewModel)DataContext;

    private void OverviewResetView_Click(object sender, RoutedEventArgs e) =>
        Vm.OverviewZoom = 1.0;

    private void OverviewScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        var step = e.Delta > 0 ? 1.12 : 1 / 1.12;
        Vm.OverviewZoom = Math.Clamp(Vm.OverviewZoom * step, 0.2, 4.0);
        e.Handled = true;
    }
}
