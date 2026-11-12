using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Map.ViewModels;

public partial class MapSearchPageModel : ObservableObject
{
    public const int ResultLimit = 100;

    private readonly IApiClient _apiClient;
    private readonly ISavedAreaSearchStore _savedAreaStore;
    private readonly IErrorHandler _errorHandler;
    private CancellationTokenSource? _searchCts;

    public MapSearchPageModel(
        IApiClient apiClient,
        ISavedAreaSearchStore savedAreaStore,
        IErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _savedAreaStore = savedAreaStore;
        _errorHandler = errorHandler;

        foreach (var area in _savedAreaStore.GetAll())
            SavedAreas.Add(area);
    }

    /// <summary>Raised when the draft polygon or the pins need to be redrawn.</summary>
    public event EventHandler? MapDataChanged;

    /// <summary>Raised after a polygon is closed so the map can fit its bounds.</summary>
    public event EventHandler? FocusRequested;

    public PolygonDraft Draft { get; } = new();

    public ObservableCollection<MapListingItem> Listings { get; } = [];

    public ObservableCollection<SavedAreaSearch> SavedAreas { get; } = [];

    public IReadOnlyList<MapSortOption> SortOptions { get; } = MapSortOption.All;

    [ObservableProperty]
    public partial bool IsDrawing { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }
        = "Chạm “Vẽ khu vực”, sau đó chạm lên bản đồ để thêm đỉnh.";

    [ObservableProperty]
    public partial string ResultsText { get; set; } = "";

    [ObservableProperty]
    public partial MapListingItem? SelectedListing { get; set; }

    [ObservableProperty]
    public partial int SelectedSortIndex { get; set; } = 1;

    [ObservableProperty]
    public partial string NewAreaName { get; set; } = "";

    [ObservableProperty]
    public partial SavedAreaSearch? SelectedSavedArea { get; set; }

    [ObservableProperty]
    public partial bool IsSavedPanelOpen { get; set; }

    public bool HasPolygon => Draft.IsClosed;

    public bool HasSelection => SelectedListing is not null;

    /// <summary>The preview and the saved-area panel share the bottom corner on phones.</summary>
    public bool ShowPreviewCard => HasSelection && !IsSavedPanelOpen;

    public bool HasResults => Listings.Count > 0;

    public bool HasSavedAreas => SavedAreas.Count > 0;

    public bool HasSelectedSavedArea => SelectedSavedArea is not null;

    public bool ShowStartDrawing => !IsDrawing;

    public bool ShowDrawingTools => IsDrawing;

    public bool CanFinishDrawing => IsDrawing && Draft.Vertices.Count >= PolygonDraft.MinimumVertices;

    public bool CanSaveCurrentArea => HasPolygon && !string.IsNullOrWhiteSpace(NewAreaName);

    public bool ShowSearchButton => HasPolygon && !IsDrawing;

    partial void OnIsDrawingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStartDrawing));
        OnPropertyChanged(nameof(ShowDrawingTools));
        OnPropertyChanged(nameof(CanFinishDrawing));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
        OnPropertyChanged(nameof(ShowSearchButton));
    }

    partial void OnSelectedListingChanged(MapListingItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowPreviewCard));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsSavedPanelOpenChanged(bool value) => OnPropertyChanged(nameof(ShowPreviewCard));

    partial void OnNewAreaNameChanged(string value) => OnPropertyChanged(nameof(CanSaveCurrentArea));

    partial void OnSelectedSavedAreaChanged(SavedAreaSearch? value) => OnPropertyChanged(nameof(HasSelectedSavedArea));

    partial void OnSelectedSortIndexChanged(int value)
    {
        if (Draft.IsClosed)
            SearchCommand.Execute(null);
    }

    public void AddVertex(double latitude, double longitude)
    {
        if (!IsDrawing || !Draft.Add(new GeoPoint(latitude, longitude)))
            return;

        StatusMessage = Draft.Vertices.Count < PolygonDraft.MinimumVertices
            ? $"Đã thêm {Draft.Vertices.Count} đỉnh · cần ít nhất {PolygonDraft.MinimumVertices} đỉnh."
            : $"Đã thêm {Draft.Vertices.Count} đỉnh · chạm “Hoàn tất” để tìm phòng.";
        NotifyDraftChanged();
    }

    public void SelectListing(MapListingItem? listing) => SelectedListing = listing;

    [RelayCommand]
    private void StartDrawing()
    {
        _searchCts?.Cancel();
        Draft.Clear();
        Listings.Clear();
        SelectedListing = null;
        ResultsText = "";
        IsDrawing = true;
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = "Chạm lên bản đồ để thêm đỉnh. Cần ít nhất 3 đỉnh, hoàn tác bằng nút bên dưới.";
        NotifyDraftChanged();
    }

    [RelayCommand]
    private void UndoVertex()
    {
        if (!Draft.RemoveLast())
            return;

        StatusMessage = Draft.Vertices.Count == 0
            ? "Chạm lên bản đồ để thêm đỉnh đầu tiên."
            : $"Đã thêm {Draft.Vertices.Count} đỉnh · cần ít nhất {PolygonDraft.MinimumVertices} đỉnh.";
        NotifyDraftChanged();
    }

    [RelayCommand]
    private async Task FinishDrawingAsync()
    {
        if (!Draft.Close())
        {
            StatusMessage = $"Cần ít nhất {PolygonDraft.MinimumVertices} đỉnh trước khi hoàn tất.";
            NotifyDraftChanged();
            return;
        }

        IsDrawing = false;
        StatusMessage = "Đang tìm phòng trong khu vực…";
        NotifyDraftChanged();
        FocusRequested?.Invoke(this, EventArgs.Empty);
        await SearchInternalAsync();
    }

    [RelayCommand]
    private void CancelDrawing()
    {
        _searchCts?.Cancel();
        Draft.Clear();
        IsDrawing = false;
        StatusMessage = "Đã xóa khu vực đang vẽ.";
        NotifyDraftChanged();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Search() => SearchInternalAsync();

    private async Task SearchInternalAsync()
    {
        if (!Draft.IsClosed)
            return;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _searchCts, cts)?.Cancel();

        IsBusy = true;
        try
        {
            var request = new AreaSearchRequest
            {
                Area = new AreaPolygon
                {
                    Type = "Polygon",
                    Coordinates = Draft.ToGeoJsonCoordinates()
                },
                Sort = SortOptions[Math.Clamp(SelectedSortIndex, 0, SortOptions.Count - 1)].WireValue,
                Limit = ResultLimit
            };

            var result = await _apiClient.SearchByAreaAsync(request, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            var centroid = Draft.Centroid;

            Listings.Clear();
            foreach (var listing in result.Data)
            {
                Listings.Add(new MapListingItem(
                    listing,
                    RoomListingService.DistanceKm(centroid.Lat, centroid.Lng, listing.Lat, listing.Lng)));
            }

            SelectedListing = null;
            ResultsText = result.TotalCountCapped
                ? $"Hiển thị {Listings.Count} / {result.TotalCount}+ phòng"
                : $"Hiển thị {Listings.Count} / {result.TotalCount} phòng";
            StatusMessage = Listings.Count == 0
                ? "Không có phòng nào trong khu vực này."
                : $"Tìm thấy {result.TotalCount} phòng trong khu vực.";
        }
        catch (OperationCanceledException)
        {
            if (!cts.IsCancellationRequested)
                StatusMessage = "Tìm kiếm quá lâu. Vui lòng thử lại.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (!cts.IsCancellationRequested)
                StatusMessage = InvalidAreaStatusMessage(ex.Code);
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested)
                return;

            StatusMessage = "Không thể tìm phòng trong khu vực. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts))
            {
                _searchCts = null;
                IsBusy = false;
                OnPropertyChanged(nameof(HasResults));
                MapDataChanged?.Invoke(this, EventArgs.Empty);
            }

            cts.Dispose();
        }
    }

    private static string InvalidAreaStatusMessage(string? code) => code switch
    {
        "area_too_large" => "Khu vực quá lớn (tối đa 100 km²). Hãy xóa và vẽ khu vực nhỏ hơn.",
        "self_intersecting_area" => "Khu vực bị tự cắt. Hãy xóa và vẽ lại.",
        "invalid_area_vertex_count" => "Khu vực có quá nhiều đỉnh (tối đa 200). Hãy xóa và vẽ lại.",
        _ => "Khu vực vẽ không hợp lệ (có thể bị tự cắt hoặc quá lớn). Hãy xóa và vẽ lại."
    };

    [RelayCommand]
    private void SaveArea()
    {
        if (!Draft.IsClosed)
        {
            StatusMessage = "Hãy hoàn tất khu vực trước khi lưu.";
            return;
        }

        var name = NewAreaName.Trim();
        if (name.Length == 0)
        {
            StatusMessage = "Nhập tên cho khu vực đã lưu.";
            return;
        }

        var saved = new SavedAreaSearch(
            Guid.NewGuid().ToString("N"),
            name,
            Draft.Vertices.ToArray(),
            DateTime.UtcNow);

        var areas = SavedAreas.Append(saved).ToArray();
        _savedAreaStore.ReplaceAll(areas);
        SavedAreas.Add(saved);
        SelectedSavedArea = saved;
        NewAreaName = "";
        StatusMessage = $"Đã lưu khu vực “{name}”.";
        OnPropertyChanged(nameof(HasSavedAreas));
    }

    [RelayCommand]
    private void ApplySavedArea()
    {
        if (SelectedSavedArea is not { } saved)
            return;

        if (!Draft.Load(saved.Vertices))
        {
            StatusMessage = $"Khu vực “{saved.Name}” không hợp lệ.";
            NotifyDraftChanged();
            return;
        }

        IsDrawing = false;
        IsSavedPanelOpen = false;
        SelectedListing = null;
        Listings.Clear();
        ResultsText = "";
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = $"Đang tìm phòng trong khu vực “{saved.Name}”…";
        NotifyDraftChanged();
        FocusRequested?.Invoke(this, EventArgs.Empty);
        SearchCommand.Execute(null);
    }

    [RelayCommand]
    private void DeleteSavedArea()
    {
        if (SelectedSavedArea is not { } saved)
            return;

        var remaining = SavedAreas.Where(area => area.Id != saved.Id).ToArray();
        _savedAreaStore.ReplaceAll(remaining);
        SavedAreas.Remove(saved);
        SelectedSavedArea = SavedAreas.FirstOrDefault();
        StatusMessage = $"Đã xóa khu vực “{saved.Name}”.";
        OnPropertyChanged(nameof(HasSavedAreas));
    }

    [RelayCommand]
    private void ClearSelection() => SelectedListing = null;

    [RelayCommand]
    private void ToggleSavedAreas() => IsSavedPanelOpen = !IsSavedPanelOpen;

    private void NotifyDraftChanged()
    {
        OnPropertyChanged(nameof(HasPolygon));
        OnPropertyChanged(nameof(CanFinishDrawing));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
        OnPropertyChanged(nameof(ShowSearchButton));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }
}
