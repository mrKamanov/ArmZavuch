using System.Windows;
using System.Windows.Controls;
using ArmZavuch.Services.Help;
using ArmZavuch.ViewModels;

namespace ArmZavuch.Views;

/// <summary>Вкладка «Инструкция»: дерево тем, поиск, текст статьи, переход в модуль.</summary>
public partial class InstructionsView : UserControl
{
    public InstructionsView(InstructionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void TopicTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not InstructionsViewModel vm || e.NewValue is not InstructionTopicNode node)
            return;

        vm.SelectTopicCommand.Execute(node);
    }
}
