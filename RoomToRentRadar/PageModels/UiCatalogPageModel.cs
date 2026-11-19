using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Pages.Controls.Cards.Models;

namespace RoomToRentRadar.PageModels;

public partial class UiCatalogPageModel : ObservableObject
{
    [ObservableProperty]
    public partial RoomCardDisplayModel SampleRoom { get; set; }

    [ObservableProperty]
    public partial bool IsButtonLoading { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FeedbackMessage { get; set; } = "Click components below to test interactivity.";

    public UiCatalogPageModel()
    {
        SampleRoom = new RoomCardDisplayModel
        {
            Id = "room-demo-01",
            Title = "Furnished Studio Room near ICTU Main Gate",
            PricePerMonth = 2200000m,
            AreaSquareMeters = 25.5,
            Address = "18 Z115 Street, Quyet Thang, Thai Nguyen City",
            DistanceMeters = 350,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=600&q=80",
            IsVerified = true,
            IsAvailable = true,
            IsBookmarked = false,
            Amenities = ["Wifi", "Air Conditioner", "Water Heater", "Private Bath"]
        };
    }

    [RelayCommand]
    private void ToggleButtonLoading()
    {
        IsButtonLoading = !IsButtonLoading;
        FeedbackMessage = IsButtonLoading ? "RadaButton is currently in IsLoading = true state" : "RadaButton finished loading state";
    }

    [RelayCommand]
    private void ToggleInputError()
    {
        InputError = string.IsNullOrEmpty(InputError)
            ? "Invalid phone number or format!"
            : string.Empty;
    }

    [RelayCommand]
    private void ToggleBookmark(string roomId)
    {
        SampleRoom = SampleRoom with { IsBookmarked = !SampleRoom.IsBookmarked };
        FeedbackMessage = SampleRoom.IsBookmarked
            ? "Saved room to Favourites (US04)!"
            : "Removed room from Favourites.";
    }

    [RelayCommand]
    private void ViewRoomDetails(string roomId)
    {
        FeedbackMessage = $"Navigating to room details: {roomId}";
    }

    [RelayCommand]
    private void RetryOperation()
    {
        FeedbackMessage = "Clicked 'Retry' on ErrorRetryView!";
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        FeedbackMessage = "Clicked 'Reset Filters' on EmptyStateView!";
    }
}
