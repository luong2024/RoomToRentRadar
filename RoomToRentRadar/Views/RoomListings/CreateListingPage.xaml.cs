using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;

namespace RoomToRentRadar.Views.RoomListings;

public partial class CreateListingPage : ContentPage
{
    private readonly CreateListingViewModel _viewModel;

    public CreateListingPage() : this(ResolveViewModel())
    {
    }

    public CreateListingPage(CreateListingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.LocationPickerHandler = OpenLocationPickerAsync;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshLocationForSession();
    }

    private Task<ListingLocationPickerResult> OpenLocationPickerAsync(GeoCoordinate? initialLocation)
    {
        var services = Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services;
        return services?.GetService<ListingLocationPickerPage>() is { } picker
            ? picker.PickAsync(this, initialLocation)
            : Task.FromResult(new ListingLocationPickerResult(ListingLocationPickerOutcome.Failed));
    }

    private static CreateListingViewModel ResolveViewModel()
    {
        if (IPlatformApplication.Current?.Services != null)
        {
            var vm = IPlatformApplication.Current.Services.GetService<CreateListingViewModel>();
            if (vm != null)
            {
                return vm;
            }

            var createService = IPlatformApplication.Current.Services.GetService<ICreateListingService>()
                                ?? new CreateListingService(IPlatformApplication.Current.Services.GetService<IApiClient>() ?? new ApiClient(new HttpClient()));
            var mediaService = IPlatformApplication.Current.Services.GetService<IMediaPickerService>()
                               ?? new MediaPickerService();
            var authSession = IPlatformApplication.Current.Services.GetService<IAuthenticationSessionService>();
            return new CreateListingViewModel(createService, mediaService, authSession);
        }

        return new CreateListingViewModel(
            new CreateListingService(new ApiClient(new HttpClient())),
            new MediaPickerService());
    }
}
