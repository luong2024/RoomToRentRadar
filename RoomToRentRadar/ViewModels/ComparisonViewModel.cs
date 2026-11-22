using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Campuses;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.ViewModels;

public partial class ComparisonViewModel : ObservableObject
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly IReadOnlyDictionary<AmenityCode, string> AmenityShortLabels =
        new Dictionary<AmenityCode, string>
        {
            [AmenityCode.Wifi] = "Wi-Fi",
            [AmenityCode.AirConditioner] = "Điều hòa",
            [AmenityCode.Parking] = "Chỗ để xe",
            [AmenityCode.WashingMachine] = "Máy giặt",
            [AmenityCode.Refrigerator] = "Tủ lạnh",
            [AmenityCode.Kitchen] = "Bếp",
            [AmenityCode.PrivateBathroom] = "WC riêng",
            [AmenityCode.Balcony] = "Ban công",
            [AmenityCode.Elevator] = "Thang máy",
            [AmenityCode.Security] = "An ninh",
            [AmenityCode.Gym] = "Gym",
            [AmenityCode.Pool] = "Hồ bơi",
            [AmenityCode.PetAllowed] = "Thú cưng"
        };

    private readonly IRoomComparisonStore _store;
    private readonly IApiClient _apiClient;
    private readonly ITravelEstimatesClient _travelClient;
    private readonly ICampusSelectionStore _campusStore;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private bool _suppressCampusSave;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedCampusName { get; set; }

    [ObservableProperty]
    public partial string DistanceNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ComparisonSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCapturing { get; set; }

    public ComparisonViewModel(
        IRoomComparisonStore store,
        IApiClient apiClient,
        ITravelEstimatesClient travelClient,
        ICampusSelectionStore campusStore,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _store = store;
        _apiClient = apiClient;
        _travelClient = travelClient;
        _campusStore = campusStore;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        CampusNames = CampusCatalog.All.Select(campus => campus.Name).ToList();
    }

    public ObservableCollection<ComparisonColumnHeader> Columns { get; } = new();

    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    public IReadOnlyList<string> CampusNames { get; }

    public int MaxRooms => _store.MaxRooms;

    public bool HasColumns => Columns.Count > 0;

    public bool ShowContent => !IsLoading && !HasError && HasColumns;

    public bool ShowEmptyState => !IsLoading && !HasError && !HasColumns;

    public bool ShowChrome => !IsCapturing;

    public bool ShowBottomBar => ShowContent && !IsCapturing;

    public bool HasDistanceNote => !string.IsNullOrEmpty(DistanceNote);

    public bool HasCampusSelection => !string.IsNullOrWhiteSpace(SelectedCampusName);

    partial void OnIsLoadingChanged(bool value) => NotifyStateChanged();

    partial void OnHasErrorChanged(bool value) => NotifyStateChanged();

    partial void OnIsCapturingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowChrome));
        OnPropertyChanged(nameof(ShowBottomBar));
    }

    partial void OnDistanceNoteChanged(string value) => OnPropertyChanged(nameof(HasDistanceNote));

    partial void OnSelectedCampusNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCampusSelection));

        if (_suppressCampusSave)
        {
            return;
        }

        _campusStore.SetSelectedCampus(value);
        LoadCommand.Execute(null);
    }

    public void OnAppearing()
    {
        _suppressCampusSave = true;
        SelectedCampusName = _campusStore.GetSelectedCampus();
        _suppressCampusSave = false;

        LoadCommand.Execute(null);
    }

    public void Cancel() => _loadCancellation?.Cancel();

    public void ReportShareFailure(Exception exception) => _errorHandler.HandleError(exception);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _loadCancellation.Token;
        var generation = ++_loadGeneration;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            await BuildAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Columns.Clear();
            Rows.Clear();
            ComparisonSummary = string.Empty;
            DistanceNote = string.Empty;
            HasError = true;
            ErrorMessage = "Chưa tải được dữ liệu so sánh. Kiểm tra kết nối rồi thử lại.";
            _errorHandler.HandleError(exception);
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RetryAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    [RelayCommand]
    private async Task RemoveRoomAsync(Guid listingId)
    {
        _store.Remove(listingId);
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        _store.Clear();
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        try
        {
            await _navigationService.GoBackAsync();
        }
        catch (Exception exception)
        {
            _errorHandler.HandleError(exception);
        }
    }

    private async Task BuildAsync(int generation, CancellationToken token)
    {
        var candidates = _store.Rooms.ToArray();

        _suppressCampusSave = true;
        SelectedCampusName ??= _campusStore.GetSelectedCampus();
        _suppressCampusSave = false;

        Columns.Clear();
        Rows.Clear();
        ComparisonSummary = string.Empty;
        DistanceNote = string.Empty;

        if (candidates.Length == 0)
        {
            OnPropertyChanged(nameof(HasColumns));
            return;
        }

        var snapshots = await Task.WhenAll(candidates.Select(candidate => LoadSnapshotAsync(candidate, token)));
        if (generation != _loadGeneration)
        {
            return;
        }

        if (snapshots.All(snapshot => snapshot.Failed))
        {
            throw new InvalidOperationException("No listing details could be loaded.");
        }

        var campus = CampusCatalog.FindByName(SelectedCampusName);
        var distances = await Task.WhenAll(snapshots.Select(snapshot => LoadDistanceAsync(snapshot, campus, token)));
        if (generation != _loadGeneration)
        {
            return;
        }

        foreach (var snapshot in snapshots)
        {
            Columns.Add(new ComparisonColumnHeader(
                snapshot.Candidate.Id,
                snapshot.Detail?.Title ?? snapshot.Candidate.Title,
                snapshot.Detail is { } detail ? FormatMoney(detail.Price) : "Không còn khả dụng",
                MediaUrlResolver.Resolve(snapshot.Candidate.CoverPhotoUrl),
                snapshot.Candidate.CoverPhotoUrl is not null));
        }

        Rows.Add(CreateRow("Giá thuê", snapshots
            .Select(snapshot => snapshot.Detail is { } detail
                ? (FormatMoney(detail.Price), (double?)detail.Price)
                : ("—", null))
            .ToArray(), higherIsBetter: false));

        Rows.Add(CreateRow("Tiền cọc", snapshots
            .Select(snapshot => snapshot.Detail is { } detail
                ? (FormatMoney(detail.Deposit), (double?)detail.Deposit)
                : ("—", null))
            .ToArray(), higherIsBetter: false));

        Rows.Add(CreateRow("Diện tích", snapshots
            .Select(snapshot => snapshot.Detail is { } detail
                ? (FormatArea(detail.AreaM2), (double?)detail.AreaM2)
                : ("—", null))
            .ToArray(), higherIsBetter: true));

        var distanceByListing = distances.ToDictionary(distance => distance.ListingId);
        Rows.Add(CreateRow("Khoảng cách", snapshots
            .Select(snapshot => DescribeDistance(distanceByListing[snapshot.Candidate.Id], campus is not null))
            .ToArray(), higherIsBetter: false));

        Rows.Add(CreateRow("Tiện nghi", snapshots
            .Select(snapshot => snapshot.Detail is { } detail
                ? DescribeAmenities(detail.Amenities)
                : ("—", (double?)null))
            .ToArray(), higherIsBetter: true));

        Rows.Add(CreateRow("Điểm an ninh", snapshots
            .Select(snapshot => snapshot.Detail is { } detail
                ? DescribeSecurity(detail.Amenities)
                : ("—", (double?)null))
            .ToArray(), higherIsBetter: true));

        ComparisonSummary = $"So sánh {Columns.Count} phòng · {Rows.Count} tiêu chí";
        DistanceNote = BuildDistanceNote(distances, snapshots, campus is not null);
        OnPropertyChanged(nameof(HasColumns));
    }

    private async Task<ListingSnapshot> LoadSnapshotAsync(ComparisonCandidate candidate, CancellationToken token)
    {
        try
        {
            var result = await _apiClient.GetListingDetailAsync(candidate.Id, token);
            return result.Outcome == ListingDetailFetchOutcome.Found && result.Detail is { } detail
                ? new ListingSnapshot(candidate, detail, Failed: false)
                : new ListingSnapshot(candidate, null, Failed: false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new ListingSnapshot(candidate, null, Failed: true);
        }
    }

    private async Task<DistanceSnapshot> LoadDistanceAsync(
        ListingSnapshot snapshot,
        CampusInfo? campus,
        CancellationToken token)
    {
        if (campus is null || snapshot.Detail is null)
        {
            return new DistanceSnapshot(snapshot.Candidate.Id, null, false, false);
        }

        try
        {
            var result = await _travelClient.GetEstimatesAsync(snapshot.Candidate.Id, campus.Id, token);
            if (result.Outcome == TravelEstimatesFetchOutcome.RateLimited)
            {
                return new DistanceSnapshot(snapshot.Candidate.Id, null, false, true);
            }

            if (result.Outcome != TravelEstimatesFetchOutcome.Found || result.Estimates is not { } estimates)
            {
                return new DistanceSnapshot(snapshot.Candidate.Id, null, false, false);
            }

            var motorbike = estimates.Modes.FirstOrDefault(mode => mode.Mode == TravelMode.Motorbike)
                ?? estimates.Modes.FirstOrDefault();

            return motorbike is null
                ? new DistanceSnapshot(snapshot.Candidate.Id, null, false, false)
                : new DistanceSnapshot(
                    snapshot.Candidate.Id,
                    motorbike.DistanceM,
                    motorbike.Source == TravelSource.Fallback,
                    false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new DistanceSnapshot(snapshot.Candidate.Id, null, false, false);
        }
    }

    private static ComparisonRow CreateRow(
        string label,
        IReadOnlyList<(string Text, double? Metric)> values,
        bool higherIsBetter)
    {
        if (values.Count < 2)
        {
            return new ComparisonRow(label, values.Select(value => new ComparisonCell(value.Text, false)).ToArray());
        }

        var metrics = values.Where(value => value.Metric.HasValue).Select(value => value.Metric!.Value).ToArray();
        double? best = metrics.Length == 0
            ? null
            : higherIsBetter ? metrics.Max() : metrics.Min();

        return new ComparisonRow(label, values
            .Select(value => new ComparisonCell(
                value.Text,
                best is { } target && value.Metric is { } metric && metric.Equals(target)))
            .ToArray());
    }

    private static (string Text, double? Metric) DescribeDistance(DistanceSnapshot distance, bool hasCampus)
    {
        if (!hasCampus)
        {
            return ("Chọn khu vực", null);
        }

        if (distance.DistanceM is not { } metres)
        {
            return ("—", null);
        }

        var text = metres < 1000
            ? $"{metres.ToString("N0", Vietnamese)} m"
            : $"{(metres / 1000d).ToString("0.0", Vietnamese)} km";

        return distance.IsFallback ? ($"{text}*", metres) : (text, metres);
    }

    private static (string Text, double? Metric) DescribeAmenities(IReadOnlyList<AmenityCode> amenities)
    {
        if (amenities.Count == 0)
        {
            return ("Chưa có", 0);
        }

        var names = amenities
            .Take(2)
            .Select(code => AmenityShortLabels.TryGetValue(code, out var label) ? label : code.ToString());
        var joined = string.Join(" · ", names);
        var extra = amenities.Count > 2 ? $" +{amenities.Count - 2}" : string.Empty;

        return ($"{amenities.Count} tiện nghi\n{joined}{extra}", amenities.Count);
    }

    private static (string Text, double? Metric) DescribeSecurity(IReadOnlyList<AmenityCode> amenities)
    {
        var result = RoomSecurityScore.Calculate(amenities);
        return ($"{result.Score}/100\n{result.Band}", result.Score);
    }

    private static string BuildDistanceNote(
        IReadOnlyList<DistanceSnapshot> distances,
        IReadOnlyList<ListingSnapshot> snapshots,
        bool hasCampus)
    {
        if (!hasCampus)
        {
            return "Chọn khu vực của bạn để so sánh khoảng cách.";
        }

        var notes = new List<string>();
        if (distances.Any(distance => distance.WasRateLimited))
        {
            notes.Add("Một số tuyến đã chạm giới hạn tính đường, thử lại sau ít phút.");
        }
        else if (distances.Any(distance => distance.IsFallback))
        {
            notes.Add("Khoảng cách có dấu * là ước tính đường thẳng.");
        }

        if (snapshots.Any(snapshot => snapshot.Detail is null && !snapshot.Failed))
        {
            notes.Add("Có phòng không còn khả dụng nên không thể so sánh đầy đủ.");
        }

        if (snapshots.Any(snapshot => snapshot.Failed))
        {
            notes.Add("Một số phòng chưa tải được, hãy thử lại.");
        }

        return string.Join(" ", notes);
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowBottomBar));
        OnPropertyChanged(nameof(HasColumns));
    }

    private static string FormatMoney(long value) => $"{value.ToString("N0", Vietnamese)} đ";

    private static string FormatArea(double value) => $"{value.ToString("N0", Vietnamese)} m²";

    private sealed record ListingSnapshot(ComparisonCandidate Candidate, ListingDetailDto? Detail, bool Failed);

    private sealed record DistanceSnapshot(Guid ListingId, int? DistanceM, bool IsFallback, bool WasRateLimited);
}
