using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

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

    public MapSearchViewModel(ISpatialSearchService spatialSearchService)
    {
        _spatialSearchService = spatialSearchService;
    }

    /// <summary>
    /// Danh sách phòng khớp vùng vẽ (kết quả của T05.1 và đầu vào cho T05.3).
    /// </summary>
    public ObservableCollection<SpatialSearchPinDto> MatchedRooms { get; } = [];

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
        set => SetProperty(ref _hasPolygon, value);
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

    [RelayCommand]
    private void ToggleDrawMode()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        IsDrawingMode = !IsDrawingMode;
    }

    [RelayCommand]
    private void ClearPolygon()
    {
        CurrentPolygonGeoJson = [];
        MatchedRooms.Clear();
        ResultCount = 0;
        HasPolygon = false;
        IsDrawingMode = false;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "Đã xóa vùng vẽ. Bấm \"Vẽ khu vực\" để khoanh lại";
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
            foreach (var item in response.Items)
            {
                MatchedRooms.Add(item);
            }

            ResultCount = response.TotalCount > 0 ? response.TotalCount : MatchedRooms.Count;
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