using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private const int PreviewLimit = 5;

    private readonly IApiClient _apiClient;
    private readonly IErrorHandler _errorHandler;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    public HomeViewModel(IApiClient apiClient, IErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        StatusMessage = "Latest listings";
    }

    public ObservableCollection<ListingSummaryDto> Listings { get; } = new();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var page = await _apiClient.GetListingsAsync(PreviewLimit, cancellationToken);
            Listings.Clear();
            foreach (var listing in page.Data)
                Listings.Add(listing);

            StatusMessage = Listings.Count == 0
                ? "No listings published yet."
                : $"{Listings.Count} latest listings";
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load listings.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
