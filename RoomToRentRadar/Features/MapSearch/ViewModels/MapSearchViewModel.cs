using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Features.MapSearch.Models;

namespace RoomToRentRadar.Features.MapSearch.ViewModels;

public partial class MapSearchViewModel : ObservableObject
{
    // Default initial location: ICTU Campus (Thai Nguyen University of Information and Communication Technology)
    public const double DefaultLatitude = 21.5855;
    public const double DefaultLongitude = 105.8075;
    public const double DefaultDistanceKm = 2.0;

    // Minimum area threshold in degrees squared (~20m x 20m area on earth)
    public const double MinimumPolygonAreaThreshold = 1e-8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(IsInteractingMode))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    [NotifyPropertyChangedFor(nameof(BackButtonText))]
    private bool _isDrawingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(IsInteractingMode))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    [NotifyPropertyChangedFor(nameof(BackButtonText))]
    private bool _isEditingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDrawing))]
    [NotifyPropertyChangedFor(nameof(ShowCompletedToolbar))]
    private bool _hasDrawnPolygon;

    public bool CanStartDrawing => !IsDrawingMode && !IsEditingMode && !HasDrawnPolygon;
    public bool IsInteractingMode => IsDrawingMode || IsEditingMode;
    public bool ShowCompletedToolbar => HasDrawnPolygon && !IsDrawingMode && !IsEditingMode;
    public string BackButtonText => IsInteractingMode ? "Back" : "Home";

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private string _statusMessage = "Tap 'Draw area' to search rooms by neighbourhood";

    [ObservableProperty]
    private bool _isAreaConfirmed;

    public DrawnPolygonArea CurrentArea { get; } = new();

    public ObservableCollection<GeoCoordinate> PolygonCoordinates { get; } = new();

    public ObservableCollection<EditableAnchorPin> EditAnchorPins { get; } = new();

    public Action? OnNavigateBackRequested { get; set; }

    public Func<string, string, string, Task>? AlertHandler { get; set; }

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
            ? "Search area defined"
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
        IsAreaConfirmed = false;
        VertexCount = 0;
        StatusMessage = "Tap 'Draw area' to search rooms by neighbourhood";
    }

    [RelayCommand]
    public void DrawAgain()
    {
        ClearPolygon();
        StartDrawing();
    }

    /// <summary>
    /// Confirms the defined search area boundary.
    /// Note: Room filtering within this polygon will be implemented in task T05.2.
    /// </summary>
    [RelayCommand]
    public async Task ConfirmAreaAsync()
    {
        if (!HasDrawnPolygon || PolygonCoordinates.Count < 3)
        {
            StatusMessage = "Please draw a valid area before confirming.";
            return;
        }

        IsAreaConfirmed = true;
        StatusMessage = $"Area confirmed with {VertexCount} vertices. Ready for search integration (T05.2).";

        if (AlertHandler != null)
        {
            await AlertHandler.Invoke(
                "Search Area Confirmed",
                $"Search boundary with {VertexCount} vertices has been recorded.\n\nRoom filtering within this polygon will be implemented in task T05.2.",
                "OK");
        }
        else if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Search Area Confirmed",
                $"Search boundary with {VertexCount} vertices has been recorded.\n\nRoom filtering within this polygon will be implemented in task T05.2.",
                "OK");
        }
    }

    /// <summary>
    /// Alias for backwards compatibility with ViewProperties command.
    /// </summary>
    [RelayCommand]
    public Task ViewPropertiesAsync() => ConfirmAreaAsync();

    public void CompleteDrawing(IEnumerable<GeoCoordinate> geoPoints)
    {
        PolygonCoordinates.Clear();
        CurrentArea.Clear();

        var pointsList = geoPoints.ToList();

        // 1. Remove consecutive near-duplicate points (jitter reduction)
        var cleanedPoints = DeduplicatePoints(pointsList);

        // 2. Validate basic vertex count
        if (cleanedPoints.Count < 3)
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "Area is too small. Please draw at least 3 distinct points.";
            IsDrawingMode = false;
            return;
        }

        // 3. Check polygon geometric area using Shoelace formula
        double area = CalculatePolygonArea(cleanedPoints);
        if (area < MinimumPolygonAreaThreshold)
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "Drawn area is too flat or small. Please draw an open enclosed loop.";
            IsDrawingMode = false;
            return;
        }

        // 4. Simplify points using the Ramer-Douglas-Peucker (RDP) algorithm
        var simplified = SimplifyPolygon(cleanedPoints, targetMaxCount: 16);

        if (simplified.Count >= 3)
        {
            foreach (var pt in simplified)
            {
                PolygonCoordinates.Add(pt);
                CurrentArea.Vertices.Add(pt);
            }

            HasDrawnPolygon = true;
            VertexCount = simplified.Count;
            StatusMessage = $"Search area defined ({VertexCount} points)";
        }
        else
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "Area could not form a valid boundary. Please try drawing again.";
        }

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
            if (index < EditAnchorPins.Count)
            {
                EditAnchorPins[index].Latitude = newCoord.Latitude;
                EditAnchorPins[index].Longitude = newCoord.Longitude;
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
    /// Filters out consecutive points that are within a microscopic distance threshold.
    /// </summary>
    public static List<GeoCoordinate> DeduplicatePoints(List<GeoCoordinate> points, double tolerance = 1e-7)
    {
        if (points.Count <= 1)
            return new List<GeoCoordinate>(points);

        var result = new List<GeoCoordinate> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            var prev = result[^1];
            var curr = points[i];
            double dLat = curr.Latitude - prev.Latitude;
            double dLon = curr.Longitude - prev.Longitude;
            if ((dLat * dLat + dLon * dLon) > (tolerance * tolerance))
            {
                result.Add(curr);
            }
        }
        return result;
    }

    /// <summary>
    /// Calculates the enclosed geographic area using the Shoelace (Gauss) formula.
    /// </summary>
    public static double CalculatePolygonArea(List<GeoCoordinate> points)
    {
        if (points.Count < 3)
            return 0.0;

        double sum = 0.0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            sum += (points[i].Longitude * points[next].Latitude) - (points[next].Longitude * points[i].Latitude);
        }

        return Math.Abs(sum) * 0.5;
    }

    /// <summary>
    /// Simplifies polygon vertices using the Ramer-Douglas-Peucker (RDP) algorithm with adaptive tolerance.
    /// </summary>
    public static List<GeoCoordinate> SimplifyPolygon(List<GeoCoordinate> points, int targetMaxCount = 16)
    {
        if (points.Count <= targetMaxCount)
            return new List<GeoCoordinate>(points);

        // Adaptive tolerance: start with ~0.00008 deg (~9m) and adjust if needed
        double epsilon = 0.00008;
        var simplified = RamerDouglasPeucker(points, epsilon);

        // If still too many points for comfortable touch pin manipulation, gradually increase epsilon
        int iterations = 0;
        while (simplified.Count > targetMaxCount && iterations < 5)
        {
            epsilon *= 1.5;
            simplified = RamerDouglasPeucker(points, epsilon);
            iterations++;
        }

        // Guarantee minimum 3 vertices if original polygon had at least 3 vertices
        if (simplified.Count < 3 && points.Count >= 3)
        {
            // Pick evenly spaced points to maintain triangular/quad base
            return PickEvenlySpaced(points, Math.Min(targetMaxCount, Math.Max(3, points.Count)));
        }

        return simplified;
    }

    /// <summary>
    /// Recursive Ramer-Douglas-Peucker algorithm implementation for geographic points.
    /// </summary>
    public static List<GeoCoordinate> RamerDouglasPeucker(List<GeoCoordinate> points, double epsilon)
    {
        if (points.Count < 3)
            return new List<GeoCoordinate>(points);

        double maxDistance = 0.0;
        int index = 0;
        int lastIndex = points.Count - 1;

        for (int i = 1; i < lastIndex; i++)
        {
            double distance = PerpendicularDistance(points[i], points[0], points[lastIndex]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                index = i;
            }
        }

        if (maxDistance > epsilon)
        {
            var firstHalf = RamerDouglasPeucker(points.GetRange(0, index + 1), epsilon);
            var secondHalf = RamerDouglasPeucker(points.GetRange(index, points.Count - index), epsilon);

            firstHalf.RemoveAt(firstHalf.Count - 1);
            firstHalf.AddRange(secondHalf);
            return firstHalf;
        }
        else
        {
            return new List<GeoCoordinate> { points[0], points[lastIndex] };
        }
    }

    /// <summary>
    /// Computes perpendicular distance from point P to line segment AB.
    /// </summary>
    public static double PerpendicularDistance(GeoCoordinate pt, GeoCoordinate lineStart, GeoCoordinate lineEnd)
    {
        double dx = lineEnd.Longitude - lineStart.Longitude;
        double dy = lineEnd.Latitude - lineStart.Latitude;

        double lineLengthSq = dx * dx + dy * dy;
        if (lineLengthSq < 1e-12)
        {
            double dpx = pt.Longitude - lineStart.Longitude;
            double dpy = pt.Latitude - lineStart.Latitude;
            return Math.Sqrt(dpx * dpx + dpy * dpy);
        }

        double num = Math.Abs(dy * pt.Longitude - dx * pt.Latitude + lineEnd.Longitude * lineStart.Latitude - lineEnd.Latitude * lineStart.Longitude);
        return num / Math.Sqrt(lineLengthSq);
    }

    private static List<GeoCoordinate> PickEvenlySpaced(List<GeoCoordinate> points, int targetCount)
    {
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

    /// <summary>
    /// Converts latitude in degrees to Spherical Mercator Y coordinate (EPSG:3857).
    /// Used for non-linear screen-to-map projection matching Google Maps.
    /// </summary>
    public static double LatitudeToMercatorY(double latDegrees)
    {
        double clampedLat = Math.Clamp(latDegrees, -85.0511, 85.0511);
        double rad = clampedLat * Math.PI / 180.0;
        return Math.Log(Math.Tan((Math.PI / 4.0) + (rad / 2.0)));
    }

    /// <summary>
    /// Converts Spherical Mercator Y coordinate back to latitude in degrees (EPSG:3857).
    /// Used for non-linear screen-to-map projection matching Google Maps.
    /// </summary>
    public static double MercatorYToLatitude(double mercatorY)
    {
        double rad = (2.0 * Math.Atan(Math.Exp(mercatorY))) - (Math.PI / 2.0);
        double deg = rad * 180.0 / Math.PI;
        return Math.Clamp(deg, -85.0511, 85.0511);
    }
}
