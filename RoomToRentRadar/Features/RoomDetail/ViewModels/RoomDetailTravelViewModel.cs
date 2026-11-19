using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Campuses;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Features.RoomDetail.ViewModels;

public enum RoomDetailTravelState
{
    NoCampus,
    Loading,
    Loaded,
    ListingGone,
    RateLimited,
    Error
}

public sealed record TravelModeEstimateItem(string ModeLabel, string ValueText, bool IsFallback)
{
    public string AccessibilityText => $"{ModeLabel}: {ValueText}";
}

public partial class RoomDetailTravelViewModel : ObservableObject
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly ITravelEstimatesClient _travelClient;
    private readonly ICampusSelectionStore _campusStore;
    private readonly IMapsDirectionsLauncher _mapsLauncher;
    private readonly IErrorHandler _errorHandler;

    private CancellationTokenSource? _loadCancellation;
    private Guid? _listingId;
    private double? _destinationLat;
    private double? _destinationLng;
    private string? _destinationLabel;
    private bool _suppressCampusChange;

    public RoomDetailTravelViewModel(
        ITravelEstimatesClient travelClient,
        ICampusSelectionStore campusStore,
        IMapsDirectionsLauncher mapsLauncher,
        IErrorHandler errorHandler)
    {
        _travelClient = travelClient;
        _campusStore = campusStore;
        _mapsLauncher = mapsLauncher;
        _errorHandler = errorHandler;
    }

    public IReadOnlyList<string> CampusNames { get; } =
        CampusCatalog.All.Select(campus => campus.Name).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoCampusVisible))]
    [NotifyPropertyChangedFor(nameof(IsLoadingVisible))]
    [NotifyPropertyChangedFor(nameof(IsLoadedVisible))]
    [NotifyPropertyChangedFor(nameof(IsListingGoneVisible))]
    [NotifyPropertyChangedFor(nameof(IsRateLimitedVisible))]
    [NotifyPropertyChangedFor(nameof(IsErrorVisible))]
    public partial RoomDetailTravelState State { get; set; }

    [ObservableProperty]
    public partial string? SelectedCampusName { get; set; }

    [ObservableProperty]
    public partial string? RateLimitedMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFallbackNote))]
    public partial IReadOnlyList<TravelModeEstimateItem> Estimates { get; set; } = [];

    public bool IsNoCampusVisible => State == RoomDetailTravelState.NoCampus;

    public bool IsLoadingVisible => State == RoomDetailTravelState.Loading;

    public bool IsLoadedVisible => State == RoomDetailTravelState.Loaded;

    public bool IsListingGoneVisible => State == RoomDetailTravelState.ListingGone;

    public bool IsRateLimitedVisible => State == RoomDetailTravelState.RateLimited;

    public bool IsErrorVisible => State == RoomDetailTravelState.Error;

    public bool HasFallbackNote => Estimates.Any(item => item.IsFallback);

    public void Initialize(
        Guid listingId,
        double destinationLat,
        double destinationLng,
        string? destinationLabel)
    {
        _destinationLat = destinationLat;
        _destinationLng = destinationLng;
        _destinationLabel = destinationLabel;

        // Reappearing while loaded keeps the previous estimates; a failed load is
        // retried and an interrupted load (page left mid-request) is restarted.
        if (_listingId == listingId
            && State is not (RoomDetailTravelState.Loading or RoomDetailTravelState.Error))
        {
            return;
        }

        _listingId = listingId;
        Estimates = [];

        _suppressCampusChange = true;
        SelectedCampusName = _campusStore.GetSelectedCampus();
        _suppressCampusChange = false;

        StartLoad();
    }

    public void Cancel() => _loadCancellation?.Cancel();

    partial void OnSelectedCampusNameChanged(string? value)
    {
        if (_suppressCampusChange)
        {
            return;
        }

        _campusStore.SetSelectedCampus(value);

        if (_listingId is not null)
        {
            StartLoad();
        }
    }

    [RelayCommand]
    private async Task OpenDirectionsAsync()
    {
        if (_destinationLat is not { } latitude || _destinationLng is not { } longitude)
        {
            return;
        }

        if (!await _mapsLauncher.OpenAsync(latitude, longitude, _destinationLabel))
        {
            _errorHandler.HandleError(new InvalidOperationException(
                "Không mở được ứng dụng bản đồ. Anh/Chị thử lại sau nhé."));
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        try
        {
            var campus = CampusCatalog.FindByName(SelectedCampusName);

            if (_listingId is not { } listingId || campus is null)
            {
                Estimates = [];
                State = RoomDetailTravelState.NoCampus;
                return;
            }

            State = RoomDetailTravelState.Loading;

            try
            {
                var result = await _travelClient.GetEstimatesAsync(
                    listingId,
                    campus.Id,
                    cancellation.Token);

                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                if (result.Outcome == TravelEstimatesFetchOutcome.NotFound)
                {
                    Estimates = [];
                    State = RoomDetailTravelState.ListingGone;
                    return;
                }

                if (result.Outcome == TravelEstimatesFetchOutcome.RateLimited)
                {
                    Estimates = [];
                    RateLimitedMessage = DescribeRateLimit(result.RetryAfter);
                    State = RoomDetailTravelState.RateLimited;
                    return;
                }

                if (result.Estimates is null)
                {
                    Estimates = [];
                    State = RoomDetailTravelState.Error;
                    return;
                }

                Estimates = result.Estimates.Modes.Select(ToItem).ToList();
                State = RoomDetailTravelState.Loaded;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer request or a page exit replaced this one.
            }
            catch (Exception)
            {
                if (cancellation.IsCancellationRequested)
                {
                    return;
                }

                // The provider guarantees a fallback, so this only reflects an unreachable API.
                Estimates = [];
                State = RoomDetailTravelState.Error;
            }
        }
        finally
        {
            // Disposing here guarantees the request that owns this source has finished.
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void StartLoad() => LoadCommand.Execute(null);

    private static string DescribeRateLimit(TimeSpan? retryAfter)
    {
        var seconds = retryAfter is { } delay
            ? (int)Math.Ceiling(delay.TotalSeconds)
            : 0;

        return seconds > 0
            ? $"Hệ thống đang nhận nhiều yêu cầu. Anh/Chị thử lại sau {seconds} giây nhé."
            : "Hệ thống đang nhận nhiều yêu cầu. Anh/Chị thử lại sau ít phút nhé.";
    }

    private static TravelModeEstimateItem ToItem(TravelEstimateDto estimate)
    {
        var modeLabel = estimate.Mode switch
        {
            TravelMode.Motorbike => "Xe máy",
            TravelMode.Bus => "Xe buýt",
            TravelMode.Walking => "Đi bộ",
            _ => estimate.Mode.ToString()
        };

        if (estimate.Source == TravelSource.Fallback)
        {
            return new TravelModeEstimateItem(
                modeLabel,
                $"≈ {FormatDistance(estimate.DistanceM)} đường thẳng",
                IsFallback: true);
        }

        var duration = estimate.DurationS is { } durationS
            ? FormatDuration(durationS)
            : FormatDistance(estimate.DistanceM);

        return new TravelModeEstimateItem(
            modeLabel,
            $"{duration} · {FormatDistance(estimate.DistanceM)}",
            IsFallback: false);
    }

    private static string FormatDuration(int seconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(seconds / 60d, MidpointRounding.AwayFromZero));

        if (minutes < 60)
        {
            return string.Create(VietnameseCulture, $"{minutes} phút");
        }

        return minutes % 60 == 0
            ? string.Create(VietnameseCulture, $"{minutes / 60} giờ")
            : string.Create(VietnameseCulture, $"{minutes / 60} giờ {minutes % 60} phút");
    }

    private static string FormatDistance(int meters)
        => meters >= 1000
            ? string.Create(VietnameseCulture, $"{meters / 1000d:0.#} km")
            : string.Create(VietnameseCulture, $"{meters} m");
}
