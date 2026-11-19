using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class MessagesPlaceholderPage : ContentPage
{
    private readonly MessagesPageModel _viewModel;

    public MessagesPlaceholderPage(MessagesPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Refresh();
    }
}