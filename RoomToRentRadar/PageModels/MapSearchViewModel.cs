using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;
using RoomToRentRadar.Utilities;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// Model ghim phòng trọ hiển thị trên bản đồ & thẻ xem trước (US05 / T05.3).
/// Pure C# để chạy trực tiếp trong RoomToRentRadarUnitTests (net10.0).
/// </summary>
public class MapPinItemModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private bool _isSelected;

    public string Id { get; set; } = string.Empty;
    public string CampusId { get; set; } = "ictu-main";
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public double DistanceMeters { get; set; }
    public string RoomType { get; set; } = "Phòng trọ";
    public string Address { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = true;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(PinBackgroundHex));
                OnPropertyChanged(nameof(PinTextColorHex));
                OnPropertyChanged(nameof(PinStrokeHex));
            }
        }
    }

    public string PriceShortLabel => string.Format(ViCulture, "{0:0.#} tr", Rent / 1_000_000m);

    public string PinBadgeText => $"📍 {PriceShortLabel}";

    public string FormattedRent => string.Format(ViCulture, "{0:N0} đ/tháng", Rent);

    public string FormattedSpecs => $"📐 {Area:0.#} m² • {RoomType} • 🛵 Cách ICTU {DistanceMeters:0}m";

    public string FormattedCoordinates => $"📍 {Latitude:0.0000}, {Longitude:0.0000}";

    public string VerifiedBadgeText => IsVerified ? "✓ Đã xác minh" : "Chờ xác minh";

    public string ResolvedImageUrl =>
        !string.IsNullOrWhiteSpace(ThumbnailUrl)
        && !ThumbnailUrl.Contains("cdn.example", StringComparison.OrdinalIgnoreCase)
            ? ThumbnailUrl
            : Id.ToLowerInvariant() switch
            {
                "room-map-001" => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80",
                "room-map-002" => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80",
                "room-map-003" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=700&q=80",
                "room-map-004" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
                "room-map-005" => "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=700&q=80",
                "room-map-006" => "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=700&q=80",
                "room-map-007" => "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=700&q=80",
                "room-map-008" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=700&q=80",
                _ => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80"
            };

    public string PinBackgroundHex => IsSelected ? "#512BD4" : "#FFFFFF";

    public string PinTextColorHex => IsSelected ? "#FFFFFF" : "#0F172A";

    public string PinStrokeHex => IsSelected ? "#F59E0B" : "#CBD5E1";

    public static MapPinItemModel FromDto(SpatialSearchPinDto dto) => new()
    {
        Id = dto.Id,
        CampusId = dto.CampusId,
        Title = dto.Title,
        Rent = dto.Rent,
        Area = dto.Area,
        DistanceMeters = dto.DistanceMeters,
        RoomType = dto.RoomType,
        Address = dto.Address,
        ThumbnailUrl = dto.ThumbnailUrl,
        IsVerified = dto.IsVerified,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude
    };
}

/// <summary>
/// Khu vực tìm kiếm đã lưu trên bản đồ (US05 / T05.3).
/// </summary>
public class SavedSearchAreaModel : ObservableObject
{
    private int _matchedRoomCount;
    private bool _isActive;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<double[]> PolygonGeoJson { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;

    public int MatchedRoomCount
    {
        get => _matchedRoomCount;
        set
        {
            if (SetProperty(ref _matchedRoomCount, value))
            {
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(ChipBackgroundHex));
                OnPropertyChanged(nameof(ChipTextColorHex));
                OnPropertyChanged(nameof(ChipStrokeHex));
            }
        }
    }

    public string DisplaySummary => MatchedRoomCount > 0 ? $"📌 {Name} ({MatchedRoomCount})" : $"📌 {Name}";

    public string ChipBackgroundHex => IsActive ? "#512BD4" : "#F1F5F9";

    public string ChipTextColorHex => IsActive ? "#FFFFFF" : "#0F172A";

    public string ChipStrokeHex => IsActive ? "#512BD4" : "#CBD5E1";
}

public partial class MapSearchViewModel : ObservableObject
{
    private readonly ISpatialSearchService _spatialSearchService;

    private bool _isDrawingMode;
    private bool _hasPolygon;
    private bool _isSearching;
    private int _resultCount;
    private string _statusMessage = "Bấm \"Vẽ khu vực\" rồi khoanh vùng quanh ICTU";
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private MapPinItemModel? _selectedPin;
    private string _savedAreaNameInput = string.Empty;
    private bool _isApplyingSavedArea;

    public MapSearchViewModel(ISpatialSearchService spatialSearchService)
    {
        _spatialSearchService = spatialSearchService;

        ToggleDrawModeCommand = new RelayCommand(ToggleDrawMode);
        ClearPolygonCommand = new RelayCommand(ClearPolygon);
        SelectPinCommand = new RelayCommand<MapPinItemModel>(SelectPin);
        SelectNextPinCommand = new RelayCommand(SelectNextPin);
        SelectPreviousPinCommand = new RelayCommand(SelectPreviousPin);
        ClosePinPreviewCommand = new RelayCommand(ClosePinPreview);
        OpenSelectedPinDetailCommand = new RelayCommand(OpenSelectedPinDetail);
        SaveCurrentAreaCommand = new RelayCommand(SaveCurrentArea);
        ApplySavedAreaCommand = new AsyncRelayCommand<SavedSearchAreaModel>(ApplySavedAreaAsync);
        RemoveSavedAreaCommand = new RelayCommand<SavedSearchAreaModel>(RemoveSavedArea);

        SeedDefaultSavedAreas();
    }

    /// <summary>
    /// Danh sách phòng khớp vùng vẽ (kết quả của T05.1 và đầu vào cho T05.3).
    /// </summary>
    public ObservableCollection<SpatialSearchPinDto> MatchedRooms { get; } = [];

    /// <summary>
    /// Danh sách ghim bản đồ hiển thị trực quan và mở Preview Card (T05.3).
    /// </summary>
    public ObservableCollection<MapPinItemModel> MapPins { get; } = [];

    /// <summary>
    /// Danh sách các khu vực polygon đã lưu để lọc nhanh quanh ICTU (T05.3).
    /// </summary>
    public ObservableCollection<SavedSearchAreaModel> SavedAreas { get; } = [];

    /// <summary>
    /// Vòng tọa độ GeoJSON [lng, lat] đã khép kín hiện tại.
    /// </summary>
    public IReadOnlyList<double[]> CurrentPolygonGeoJson { get; private set; } = [];

    public bool IsDrawingMode
    {
        get => _isDrawingMode;
        set
        {
            if (SetProperty(ref _isDrawingMode, value))
            {
                OnPropertyChanged(nameof(DrawToggleButtonText));
                StatusMessage = value
                    ? "Rê ngón tay trên bản đồ để khoanh khu vực tìm phòng"
                    : HasPolygon
                        ? $"Tìm thấy {ResultCount} phòng trong vùng đã vẽ"
                        : "Bấm \"Vẽ khu vực\" rồi khoanh vùng quanh ICTU";
            }
        }
    }

    public bool HasPolygon
    {
        get => _hasPolygon;
        set
        {
            if (SetProperty(ref _hasPolygon, value))
            {
                OnPropertyChanged(nameof(CanSaveCurrentArea));
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public int ResultCount
    {
        get => _resultCount;
        set => SetProperty(ref _resultCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string DrawToggleButtonText => IsDrawingMode ? "Đang vẽ..." : "Vẽ khu vực";

    public MapPinItemModel? SelectedPin
    {
        get => _selectedPin;
        set
        {
            if (SetProperty(ref _selectedPin, value))
            {
                foreach (var pin in MapPins)
                {
                    pin.IsSelected = ReferenceEquals(pin, value);
                }

                OnPropertyChanged(nameof(HasSelectedPin));
                OnPropertyChanged(nameof(SelectedPinPositionText));
            }
        }
    }

    public bool HasSelectedPin => SelectedPin is not null;

    public bool HasMatchedPins => MapPins.Count > 0;

    public bool CanSaveCurrentArea => HasPolygon && CurrentPolygonGeoJson.Count >= 4;

    public bool HasSavedAreas => SavedAreas.Count > 0;

    public string SavedAreaNameInput
    {
        get => _savedAreaNameInput;
        set => SetProperty(ref _savedAreaNameInput, value);
    }

    public string? LastOpenedRoomDetailId { get; private set; }

    public string SelectedPinPositionText
    {
        get
        {
            if (SelectedPin is null || MapPins.Count == 0)
            {
                return string.Empty;
            }

            var index = MapPins.IndexOf(SelectedPin);
            return index >= 0 ? $"Ghim {index + 1}/{MapPins.Count}" : string.Empty;
        }
    }

    public event Action? MapPinsChanged;
    public event Action<IReadOnlyList<double[]>>? SavedAreaPolygonApplied;
    public event Action<MapPinItemModel>? RoomDetailNavigationRequested;

    public IRelayCommand ToggleDrawModeCommand { get; }
    public IRelayCommand ClearPolygonCommand { get; }
    public IRelayCommand<MapPinItemModel> SelectPinCommand { get; }
    public IRelayCommand SelectNextPinCommand { get; }
    public IRelayCommand SelectPreviousPinCommand { get; }
    public IRelayCommand ClosePinPreviewCommand { get; }
    public IRelayCommand OpenSelectedPinDetailCommand { get; }
    public IRelayCommand SaveCurrentAreaCommand { get; }
    public IAsyncRelayCommand<SavedSearchAreaModel> ApplySavedAreaCommand { get; }
    public IRelayCommand<SavedSearchAreaModel> RemoveSavedAreaCommand { get; }

    private void ToggleDrawMode()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        IsDrawingMode = !IsDrawingMode;
    }

    private void ClearPolygon()
    {
        CurrentPolygonGeoJson = [];
        MatchedRooms.Clear();
        MapPins.Clear();
        SelectedPin = null;
        ResultCount = 0;
        HasPolygon = false;
        IsDrawingMode = false;
        HasError = false;
        ErrorMessage = string.Empty;

        foreach (var savedArea in SavedAreas)
        {
            savedArea.IsActive = false;
        }

        OnPropertyChanged(nameof(HasMatchedPins));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
        OnPropertyChanged(nameof(SelectedPinPositionText));
        StatusMessage = "Đã xóa vùng vẽ. Bấm \"Vẽ khu vực\" để khoanh lại";
        MapPinsChanged?.Invoke();
    }

    private void SelectPin(MapPinItemModel? pin)
    {
        SelectedPin = pin;
    }

    public void SelectPinById(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        var match = MapPins.FirstOrDefault(p => string.Equals(p.Id, roomId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            SelectedPin = match;
        }
    }

    private void SelectNextPin()
    {
        if (MapPins.Count == 0)
        {
            return;
        }

        if (SelectedPin is null)
        {
            SelectedPin = MapPins[0];
            return;
        }

        var currentIndex = MapPins.IndexOf(SelectedPin);
        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % MapPins.Count;
        SelectedPin = MapPins[nextIndex];
    }

    private void SelectPreviousPin()
    {
        if (MapPins.Count == 0)
        {
            return;
        }

        if (SelectedPin is null)
        {
            SelectedPin = MapPins[0];
            return;
        }

        var currentIndex = MapPins.IndexOf(SelectedPin);
        var prevIndex = currentIndex <= 0 ? MapPins.Count - 1 : currentIndex - 1;
        SelectedPin = MapPins[prevIndex];
    }

    private void ClosePinPreview()
    {
        SelectedPin = null;
    }

    private void OpenSelectedPinDetail()
    {
        if (SelectedPin is null)
        {
            return;
        }

        LastOpenedRoomDetailId = SelectedPin.Id;
        OnPropertyChanged(nameof(LastOpenedRoomDetailId));
        StatusMessage = $"Đang mở chi tiết phòng {SelectedPin.Title}";
        RoomDetailNavigationRequested?.Invoke(SelectedPin);
    }

    private void SaveCurrentArea()
    {
        if (!CanSaveCurrentArea)
        {
            StatusMessage = "Hãy vẽ hoặc chọn một khu vực trước khi lưu.";
            return;
        }

        var areaName = string.IsNullOrWhiteSpace(SavedAreaNameInput)
            ? $"Khu vực đã lưu #{SavedAreas.Count + 1}"
            : SavedAreaNameInput.Trim();

        foreach (var existing in SavedAreas)
        {
            existing.IsActive = false;
        }

        var clonedRing = CurrentPolygonGeoJson
            .Select(pt => (double[])pt.Clone())
            .ToList();

        var newArea = new SavedSearchAreaModel
        {
            Id = $"area-{Guid.NewGuid():N}",
            Name = areaName,
            PolygonGeoJson = clonedRing,
            MatchedRoomCount = ResultCount > 0 ? ResultCount : MapPins.Count,
            SavedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        SavedAreas.Insert(0, newArea);
        SavedAreaNameInput = string.Empty;
        OnPropertyChanged(nameof(HasSavedAreas));
        StatusMessage = $"Đã lưu khu vực \"{areaName}\" ({newArea.MatchedRoomCount} phòng).";
    }

    private async Task ApplySavedAreaAsync(SavedSearchAreaModel? area)
    {
        if (area is null || area.PolygonGeoJson.Count < 4)
        {
            return;
        }

        foreach (var existing in SavedAreas)
        {
            existing.IsActive = ReferenceEquals(existing, area);
        }

        _isApplyingSavedArea = true;
        try
        {
            await ExecuteSpatialSearchAsync(area.PolygonGeoJson);
            area.MatchedRoomCount = ResultCount;
            area.IsActive = true;
            SavedAreaPolygonApplied?.Invoke(area.PolygonGeoJson);
            StatusMessage = $"Đã áp dụng khu vực \"{area.Name}\" • {ResultCount} phòng";
        }
        finally
        {
            _isApplyingSavedArea = false;
        }
    }

    private void RemoveSavedArea(SavedSearchAreaModel? area)
    {
        if (area is null)
        {
            return;
        }

        SavedAreas.Remove(area);
        OnPropertyChanged(nameof(HasSavedAreas));
        StatusMessage = $"Đã xóa khu vực \"{area.Name}\".";
    }

    private void SeedDefaultSavedAreas()
    {
        var ictuZ115Ring = GeoPolygonHelper.BuildClosedGeoJsonRing(
        [
            (21.5825, 105.8060),
            (21.5875, 105.8060),
            (21.5875, 105.8095),
            (21.5825, 105.8095)
        ]);

        var allIctuRing = GeoPolygonHelper.BuildClosedGeoJsonRing(
        [
            (21.5800, 105.8000),
            (21.5900, 105.8000),
            (21.5900, 105.8120),
            (21.5800, 105.8120)
        ]);

        SavedAreas.Add(new SavedSearchAreaModel
        {
            Id = "area-preset-z115",
            Name = "Cổng chính ICTU & Đường Z115",
            PolygonGeoJson = ictuZ115Ring,
            MatchedRoomCount = 3,
            IsActive = false
        });

        SavedAreas.Add(new SavedSearchAreaModel
        {
            Id = "area-preset-ictu-all",
            Name = "Toàn khu vực quanh ĐH CNTT&TT",
            PolygonGeoJson = allIctuRing,
            MatchedRoomCount = 8,
            IsActive = false
        });

        OnPropertyChanged(nameof(HasSavedAreas));
    }

    public async Task ExecuteSpatialSearchAsync(
        IReadOnlyList<double[]> geoJsonRing,
        CancellationToken cancellationToken = default)
    {
        if (geoJsonRing.Count < 4)
        {
            IsDrawingMode = false;
            StatusMessage = "Vùng vẽ cần ít nhất 3 điểm. Vui lòng vẽ lại rộng hơn.";
            return;
        }

        if (!_isApplyingSavedArea)
        {
            foreach (var existing in SavedAreas)
            {
                existing.IsActive = false;
            }
        }

        try
        {
            IsSearching = true;
            HasError = false;
            ErrorMessage = string.Empty;
            CurrentPolygonGeoJson = geoJsonRing;
            HasPolygon = true;
            IsDrawingMode = false;
            StatusMessage = "Đang tìm phòng trong vùng đã vẽ...";

            var response = await _spatialSearchService.SearchInPolygonAsync(
                geoJsonRing,
                "distance",
                cancellationToken);

            MatchedRooms.Clear();
            MapPins.Clear();
            foreach (var item in response.Items)
            {
                MatchedRooms.Add(item);
                MapPins.Add(MapPinItemModel.FromDto(item));
            }

            SelectedPin = MapPins.FirstOrDefault();
            ResultCount = response.TotalCount > 0 ? response.TotalCount : MatchedRooms.Count;

            OnPropertyChanged(nameof(HasMatchedPins));
            OnPropertyChanged(nameof(CanSaveCurrentArea));
            OnPropertyChanged(nameof(SelectedPinPositionText));
            MapPinsChanged?.Invoke();

            StatusMessage = ResultCount > 0
                ? $"Tìm thấy {ResultCount} phòng trong vùng đã khoanh"
                : "Không có phòng nào trong vùng vừa vẽ. Thử khoanh rộng hơn quanh ICTU.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HasError = true;
            ErrorMessage = $"Lỗi tìm kiếm khu vực: {ex.Message}";
            StatusMessage = "Không thể tìm kiếm khu vực vừa vẽ.";
        }
        finally
        {
            IsSearching = false;
        }
    }
}
