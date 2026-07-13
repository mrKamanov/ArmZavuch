using ArmZavuch.ViewModels;

namespace ArmZavuch.Views;

public partial class RoomsView
{
    public RoomsView(RoomsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
