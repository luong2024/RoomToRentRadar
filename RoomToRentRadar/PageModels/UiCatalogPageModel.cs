using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Converters;
using RoomToRentRadar.Resources.Strings;
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

    // Section 8: Culture-aware formatting demo state (local preview; never mutates global culture)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppCultureName))]
    [NotifyPropertyChangedFor(nameof(CultureStatusText))]
    public partial CultureInfo AppCulture { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCultureName))]
    [NotifyPropertyChangedFor(nameof(IsVietnamesePreview))]
    [NotifyPropertyChangedFor(nameof(IsEnglishPreview))]
    [NotifyPropertyChangedFor(nameof(CultureStatusText))]
    [NotifyPropertyChangedFor(nameof(PreviewModeText))]
    public partial CultureInfo PreviewCulture { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualOverride))]
    [NotifyPropertyChangedFor(nameof(PreviewModeText))]
    [NotifyPropertyChangedFor(nameof(CultureStatusText))]
    public partial bool IsUsingAppCulture { get; set; } = true;

    public bool IsManualOverride => !IsUsingAppCulture;
    public bool IsVietnamesePreview => CultureFormatting.IsVietnamese(PreviewCulture);
    public bool IsEnglishPreview => !IsVietnamesePreview;
    public string AppCultureName => GetDisplayCultureName(AppCulture);
    public string PreviewCultureName => GetDisplayCultureName(PreviewCulture);
    public string this[string key] => AppResources.GetString("Catalog_" + key, PreviewCulture);
    public string CultureStatusText => string.Format(PreviewCulture, this["Status"], AppCultureName, PreviewCultureName);
    public string PreviewModeText => this[IsUsingAppCulture ? "Following" : "Override"];

    // Refresh indexer bindings and input captions when only the preview language changes.
    partial void OnPreviewCultureChanged(CultureInfo value) => OnPropertyChanged(string.Empty);

    // Fixed typed demo inputs for deterministic converter previews
    public decimal DemoCurrencyAmount { get; } = 2200000m;
    public double DemoNumberValue { get; } = 12345.67;
    public DateOnly DemoDateValue { get; } = new(2026, 11, 25);
    public TimeOnly DemoTimeValue { get; } = new(16, 30);
    public double DemoAreaValue { get; } = 25.5;
    public double DemoDistanceValue { get; } = 1500d;

    // Invariant input labels for side-by-side verification
    public string DemoCurrencyInputText => $"{this["Input"]} {DemoCurrencyAmount.ToString(CultureInfo.InvariantCulture)}";
    public string DemoNumberInputText => $"{this["Input"]} {DemoNumberValue.ToString(CultureInfo.InvariantCulture)}";
    public string DemoDateInputText => $"{this["Input"]} {DemoDateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    public string DemoTimeInputText => $"{this["Input"]} {DemoTimeValue.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    public string DemoAreaInputText => $"{this["Input"]} {DemoAreaValue.ToString(CultureInfo.InvariantCulture)} m²";
    public string DemoDistanceInputText => $"{this["Input"]} {DemoDistanceValue.ToString(CultureInfo.InvariantCulture)} m";

    public UiCatalogPageModel()
    {
        var initialCulture = GetCurrentAppCulture();
        AppCulture = initialCulture;
        PreviewCulture = initialCulture;

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
            RoomType = RoomToRentRadar.Domain.Enums.RoomType.MiniApartment,
            Furnishing = RoomToRentRadar.Domain.Enums.Furnishing.Full,
            Amenities = ["Wifi", "Air Conditioner", "Water Heater", "Private Bath"]
        };
    }

    /// <summary>
    /// Synchronizes with the live app culture and resets any manual preview override
    /// whenever the catalog page appears (so language changes made in Account are visible immediately).
    /// </summary>
    public void OnAppearing()
    {
        SyncWithAppCulture(resetOverride: true);
    }

    /// <summary>
    /// Handles live app culture notifications while the catalog page is visible.
    /// Updates <see cref="PreviewCulture"/> automatically only when following app culture.
    /// </summary>
    public void OnAppCultureChanged()
    {
        SyncWithAppCulture(resetOverride: false);
    }

    [RelayCommand]
    private void SelectPreviewCulture(string? cultureCode)
    {
        var targetCulture = CultureFormatting.ResolveCulture(cultureCode, AppCulture);
        IsUsingAppCulture = false;
        PreviewCulture = targetCulture;
        FeedbackMessage = string.Format(PreviewCulture, this["PreviewFeedback"], PreviewCultureName, AppCultureName);
    }

    [RelayCommand]
    private void UseAppCulture()
    {
        SyncWithAppCulture(resetOverride: true);
        FeedbackMessage = string.Format(PreviewCulture, this["FollowFeedback"], AppCultureName);
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

    private void SyncWithAppCulture(bool resetOverride)
    {
        var current = GetCurrentAppCulture();
        AppCulture = current;

        if (resetOverride)
        {
            IsUsingAppCulture = true;
            PreviewCulture = current;
        }
        else if (IsUsingAppCulture)
        {
            PreviewCulture = current;
        }
    }

    private static CultureInfo GetCurrentAppCulture() =>
        CultureInfo.CurrentCulture;

    private static string GetDisplayCultureName(CultureInfo culture) =>
        string.IsNullOrWhiteSpace(culture.Name) ? CultureFormatting.EnglishCulture.Name : culture.Name;
}
