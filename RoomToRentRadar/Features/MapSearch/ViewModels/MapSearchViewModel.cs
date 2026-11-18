using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Features.MapSearch.Models;

namespace RoomToRentRadar.Features.MapSearch.ViewModels;

public partial class MapSearchViewModel : ObservableObject
{
    // Toạ độ mặc định: Trường ĐH CNTT & TT Thái Nguyên (ICTU)
    public const double DefaultLatitude = 21.5855;
    public const double DefaultLongitude = 105.8075;
    public const double DefaultDistanceKm = 2.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(IsInteractingMode))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    private bool _isDrawingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(IsInteractingMode))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    private bool _isEditingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    private bool _hasDrawnPolygon;

    public bool CanStartDrawing => !IsDrawingMode && !IsEditingMode && !HasDrawnPolygon;
    public bool IsInteractingMode => IsDrawingMode || IsEditingMode;
    public bool ShowCompletedToolbar => HasDrawnPolygon && !IsDrawingMode && !IsEditingMode;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private string _statusMessage = "Tap 'Draw area' to search rooms by neighbourhood";

    public DrawnPolygonArea CurrentArea { get; } = new();

    public ObservableCollection<GeoCoordinate> PolygonCoordinates { get; } = new();

    public ObservableCollection<EditableAnchorPin> EditAnchorPins { get; } = new();

    public MapSearchViewModel()
    {
    }

    [RelayCommand]
    public void StartDrawing()
    {
        IsEditingMode = false;
        IsDrawingMode = true;
        StatusMessage = "Draw a freehand loop around the area you care about";
    }

    [RelayCommand]
    public void StartEditing()
    {
        if (!HasDrawnPolygon)
            return;

        IsDrawingMode = false;
        IsEditingMode = true;
        BuildEditAnchors();
        StatusMessage = "Drag pins to adjust boundary shape";
    }

    [RelayCommand]
    public void FinishEditing()
    {
        IsEditingMode = false;
        StatusMessage = "Boundary updated";
    }

    public Action? OnNavigateBackRequested { get; set; }

    [RelayCommand]
    public void HandleBack()
    {
        if (IsDrawingMode)
        {
            IsDrawingMode = false;
        }
        else if (IsEditingMode)
        {
            IsEditingMode = false;
        }
        else
        {
            OnNavigateBackRequested?.Invoke();
        }
    }

    [RelayCommand]
    public void CancelDrawing()
    {
        IsDrawingMode = false;
        IsEditingMode = false;
        StatusMessage = HasDrawnPolygon
            ? "Search area drawn successfully"
            : "Tap 'Draw area' to search rooms by neighbourhood";
    }

    [RelayCommand]
    public void ClearPolygon()
    {
        CurrentArea.Clear();
        PolygonCoordinates.Clear();
        EditAnchorPins.Clear();
        HasDrawnPolygon = false;
        IsDrawingMode = false;
        IsEditingMode = false;
        VertexCount = 0;
        StatusMessage = "Tap 'Draw area' to search rooms by neighbourhood";
    }

    [RelayCommand]
    public void DrawAgain()
    {
        ClearPolygon();
        StartDrawing();
    }

    public void CompleteDrawing(IEnumerable<GeoCoordinate> geoPoints)
    {
        PolygonCoordinates.Clear();
        CurrentArea.Clear();

        var pointsList = geoPoints.ToList();
        if (pointsList.Count >= 3)
        {
            // Tự động rút gọn số đỉnh để hình đa giác gọn và tối ưu (Rightmove style)
            var simplified = SimplifyPolygon(pointsList, 16);

            foreach (var pt in simplified)
            {
                PolygonCoordinates.Add(pt);
                CurrentArea.Vertices.Add(pt);
            }

            HasDrawnPolygon = true;
            VertexCount = simplified.Count;
            StatusMessage = $"Area bounded successfully ({VertexCount} points)";
        }
        else
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "Area is too small. Please draw at least 3 points to form a polygon.";
        }

        // Tắt chế độ vẽ sau khi đã cập nhật HasDrawnPolygon để kích hoạt ShowCompletedToolbar
        IsDrawingMode = false;
    }

    public void UpdateAnchorPosition(int index, GeoCoordinate newCoord)
    {
        if (index >= 0 && index < PolygonCoordinates.Count)
        {
            PolygonCoordinates[index] = newCoord;
            if (index < CurrentArea.Vertices.Count)
            {
                CurrentArea.Vertices[index] = newCoord;
            }
        }
    }

    private void BuildEditAnchors()
    {
        EditAnchorPins.Clear();
        for (int i = 0; i < PolygonCoordinates.Count; i++)
        {
            var p = PolygonCoordinates[i];
            EditAnchorPins.Add(new EditableAnchorPin(i, p.Latitude, p.Longitude));
        }
    }

    /// <summary>
    /// Rút gọn số điểm vuốt tự do (80+ điểm) về một số lượng đỉnh chốt hợp lý (~12-16 điểm)
    /// </summary>
    private static List<GeoCoordinate> SimplifyPolygon(List<GeoCoordinate> points, int targetCount)
    {
        if (points.Count <= targetCount)
            return points;

        var result = new List<GeoCoordinate>();
        double step = (double)points.Count / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            int idx = (int)(i * step);
            if (idx < points.Count)
            {
                result.Add(points[idx]);
            }
        }

        return result;
    }
}
