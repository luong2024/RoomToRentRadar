using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadar.Features.Home.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SizeChanged += (_, _) => UpdateLayout();
    }

    private void UpdateLayout()
    {
        var inset = Width >= 720 ? 40 : 20;
        TopBar.Padding = new Thickness(inset, 18);
        ListingRefresh.Margin = new Thickness(inset, 0, inset - 18, 0);
        var contentWidth = Math.Min(Width - inset * 2, 1280);
        CardsLayout.Span = contentWidth >= 1040 ? 3 : contentWidth >= 660 ? 2 : 1;
        HeroArt.IsVisible = Width >= 850;
        NavCaption.IsVisible = Width >= 850;
        HeroTitle.FontSize = Width >= 720 ? 34 : 30;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLayout();

        if (_viewModel.Listings.Count == 0 && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
