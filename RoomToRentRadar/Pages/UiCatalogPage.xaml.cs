using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class UiCatalogPage : ContentPage
{
    public UiCatalogPage(UiCatalogPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

