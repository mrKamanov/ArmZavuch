using ArmZavuch.ViewModels;

namespace ArmZavuch.Views;

public partial class DispatcherView
{
    public DispatcherView(DispatcherViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private DispatcherViewModel Vm => (DispatcherViewModel)DataContext;

    private void DayEditorScroll_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.ScrollViewer)
            Vm.ClearDayEditorSelectionCommand.Execute(null);
    }

    private async void DayNoteBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm.SaveDayNoteCommand.CanExecute(null))
            await Vm.SaveDayNoteCommand.ExecuteAsync(null);
    }
}
