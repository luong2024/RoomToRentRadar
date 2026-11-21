using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Views.RoomListings;

public partial class CreateListingPage : ContentPage
{
    public CreateListingPage() : this(ResolveViewModel())
    {
    }

    public CreateListingPage(CreateListingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
            return new CreateListingViewModel(createService, mediaService);
        }

        return new CreateListingViewModel(
            new CreateListingService(new ApiClient(new HttpClient())),
            new MediaPickerService());
    }
}
