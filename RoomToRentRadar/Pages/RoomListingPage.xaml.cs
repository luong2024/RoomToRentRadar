namespace RoomToRentRadar.Pages;

public partial class RoomListingPage : ContentPage
{
    private readonly RoomListingPageModel _model;

    public RoomListingPage(RoomListingPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
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

    private void CampusPickerLoaded(object? sender, EventArgs e)
    {
        // Native pickers can clear SelectedItem while their ItemsSource is attaching.
        if (sender is Picker { SelectedItem: null } picker)
            picker.SelectedItem = _model.SelectedCampus ?? _model.Campuses[0];
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateLayout();
        await _model.InitializeCommand.ExecuteAsync(null);
    }
}
