using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArmZavuch.Models;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views.Controls;

/// <summary>Строка класса в сетке конструктора дня с выбором по клику на заголовок.</summary>
public partial class DayEditorGridRowView
{
    public DayEditorGridRowView() => InitializeComponent();

    private void ClassHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DispatcherMonitorRow row)
            return;

        for (var node = (DependencyObject)this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is DispatcherView view && view.DataContext is DispatcherViewModel vm)
            {
                vm.SelectDayEditorClassCommand.Execute(row);
                break;
            }
        }

        e.Handled = true;
    }
}
