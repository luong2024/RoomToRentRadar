using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RoleSelectionPage : ContentPage
{
    public RoleSelectionPage(RoleSelectionPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

