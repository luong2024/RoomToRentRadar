namespace RoomToRentRadar.Pages;

public partial class MainLandingPage : ContentPage
{
    private readonly PageModels.MainLandingPageModel _viewModel;

    public MainLandingPage(PageModels.MainLandingPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
#if DEBUG
        BuildDebugScenarioStrip();
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

#if DEBUG
    // Debug-only scenario switcher; hard-coded English literals are an approved exception for this strip.
    private void BuildDebugScenarioStrip()
    {
        var strip = new FlexLayout
        {
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        foreach (var scenario in Enum.GetValues<DeviceAdapters.DebugScenarioRoomGateway.RoomScenario>())
        {
            var button = new Button
            {
                Text = scenario.ToString(),
                HeightRequest = 48,
                Padding = new Thickness(12, 0),
                Margin = new Thickness(4)
            };
            button.Clicked += (_, _) => DeviceAdapters.DebugScenarioRoomGateway.Scenario = scenario;
            strip.Children.Add(button);
        }

        var reload = new Button
        {
            Text = "Reload",
            HeightRequest = 48,
            Padding = new Thickness(12, 0),
            Margin = new Thickness(4)
        };
        reload.Clicked += async (_, _) => await _viewModel.InitializeAsync();
        strip.Children.Add(reload);

        DebugStripHost.Children.Add(strip);
    }
#endif
}