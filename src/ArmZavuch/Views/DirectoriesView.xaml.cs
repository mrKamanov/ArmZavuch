using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views;

public partial class DirectoriesView
{
    public DirectoriesView(DirectoriesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.ActivateAsync();
    }

    /// <summary>
    /// Кнопка в заголовке Expander (корзина класса). Handled гасит ToggleButton заголовка,
    /// иначе Expander сворачивается; Command вручную — иначе Click не доходит до кнопки.
    /// </summary>
    private void ExpanderActionButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button button)
            return;

        e.Handled = true;
        if (button.Command?.CanExecute(button.CommandParameter) == true)
            button.Command.Execute(button.CommandParameter);
    }
}
