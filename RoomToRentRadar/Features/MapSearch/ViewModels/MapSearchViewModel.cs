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

    /// <summary>
    /// Minimum enclosed search area in square meters (approx 20m x 20m = 400 m²).
    /// </summary>
    public const double MinimumPolygonAreaSquareMeters = 400.0;

    /// <summary>
    /// Backwards compatibility alias for MinimumPolygonAreaSquareMeters.
    /// </summary>
    public const double MinimumPolygonAreaThreshold = MinimumPolygonAreaSquareMeters;

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

        // 1. Normalize points: remove consecutive near-duplicates and strip duplicate closing vertex
        var cleanedPoints = NormalizePolygonPoints(pointsList);

        // 2. Validate distinct vertex count
        var distinctOriginal = GetDistinctVertices(cleanedPoints);
        if (distinctOriginal.Count < 3)
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "Area is too small. Please draw at least 3 distinct points.";
            IsDrawingMode = false;
            return;
        }

        // 3. Validate against self-intersection (e.g. bow-tie / figure-8 polygon)
        if (IsSelfIntersecting(distinctOriginal))
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = "The drawn area crosses over itself. Please draw a simple loop without intersecting lines.";
            IsDrawingMode = false;
            return;
        }

        // 4. Check polygon geometric area using localized metric projection Shoelace formula (m²)
        double initialAreaM2 = CalculatePolygonAreaSquareMeters(distinctOriginal);
        if (initialAreaM2 < MinimumPolygonAreaSquareMeters)
        {
            HasDrawnPolygon = false;
            VertexCount = 0;
            StatusMessage = $"Drawn area is too flat or small ({initialAreaM2:F0} m²). Minimum required area is {MinimumPolygonAreaSquareMeters:F0} m².";
            IsDrawingMode = false;
            return;
        }

        // 5. Simplify points using the closed-loop Ramer-Douglas-Peucker (RDP) algorithm
        var simplified = SimplifyPolygon(cleanedPoints, targetMaxCount: 16);

        // 6. Post-RDP validation: ensure at least 3 distinct vertices, non-intersecting, and non-degenerate area
        var distinctSimplified = GetDistinctVertices(simplified);
        double simplifiedAreaM2 = CalculatePolygonAreaSquareMeters(distinctSimplified);

        if (distinctSimplified.Count >= 3 &&
            !IsSelfIntersecting(distinctSimplified) &&
            simplifiedAreaM2 >= MinimumPolygonAreaSquareMeters)
        {
            foreach (var pt in distinctSimplified)
            {
                PolygonCoordinates.Add(pt);
                CurrentArea.Vertices.Add(pt);
            }

            HasDrawnPolygon = true;
            VertexCount = distinctSimplified.Count;
            StatusMessage = $"Search area defined ({VertexCount} points, {simplifiedAreaM2:F0} m²)";
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
    /// Checks whether two geographic coordinates represent the same location within a tolerance.
    /// </summary>
    public static bool IsSameLocation(GeoCoordinate a, GeoCoordinate b, double tolerance = 1e-6)
    {
        return Math.Abs(a.Latitude - b.Latitude) <= tolerance &&
               Math.Abs(a.Longitude - b.Longitude) <= tolerance;
    }

    /// <summary>
    /// Normalizes raw drawn polygon points by removing consecutive jitter duplicates
    /// and stripping any duplicate closing vertex (matching start point).
    /// </summary>
    public static List<GeoCoordinate> NormalizePolygonPoints(List<GeoCoordinate> points, double tolerance = 1e-6)
    {
        if (points == null || points.Count == 0)
            return new List<GeoCoordinate>();

        var result = DeduplicatePoints(points, tolerance);

        // Strip duplicate closing point if start and end coincide
        if (result.Count >= 2 && IsSameLocation(result[0], result[^1], tolerance))
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    /// <summary>
    /// Returns the subset of distinct vertices based on geometric proximity tolerance.
    /// </summary>
    public static List<GeoCoordinate> GetDistinctVertices(IEnumerable<GeoCoordinate> points, double tolerance = 1e-6)
    {
        var distinct = new List<GeoCoordinate>();
        foreach (var pt in points)
        {
            if (!distinct.Any(d => IsSameLocation(d, pt, tolerance)))
            {
                distinct.Add(pt);
            }
        }
        return distinct;
    }

    /// <summary>
    /// Checks whether the polygon self-intersects (e.g. bow-tie or figure-8 shape).
    /// Returns true if any non-adjacent edges cross each other.
    /// </summary>
    public static bool IsSelfIntersecting(IReadOnlyList<GeoCoordinate> vertices)
    {
        if (vertices == null || vertices.Count < 4)
            return false; // A triangle cannot self-intersect

        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var a1 = vertices[i];
            var a2 = vertices[(i + 1) % n];

            for (int j = i + 1; j < n; j++)
            {
                // Adjacent edges share an endpoint, skip them
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == n - 1))
                    continue;

                var b1 = vertices[j];
                var b2 = vertices[(j + 1) % n];

                if (DoSegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool DoSegmentsIntersect(GeoCoordinate p1, GeoCoordinate q1, GeoCoordinate p2, GeoCoordinate q2)
    {
        int o1 = GetOrientation(p1, q1, p2);
        int o2 = GetOrientation(p1, q1, q2);
        int o3 = GetOrientation(p2, q2, p1);
        int o4 = GetOrientation(p2, q2, q1);

        // General case: segments cross each other
        if (o1 != o2 && o3 != o4)
            return true;

        // Special collinear cases:
        if (o1 == 0 && IsPointOnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && IsPointOnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && IsPointOnSegment(p2, p1, q2)) return true;
        if (o4 == 0 && IsPointOnSegment(p2, q1, q2)) return true;

        return false;
    }

    private static int GetOrientation(GeoCoordinate p, GeoCoordinate q, GeoCoordinate r)
    {
        double val = (q.Longitude - p.Longitude) * (r.Latitude - p.Latitude) -
                     (q.Latitude - p.Latitude) * (r.Longitude - p.Longitude);

        if (Math.Abs(val) < 1e-12) return 0; // Collinear
        return (val > 0) ? 1 : 2; // 1 = Clockwise / CCW, 2 = Opposite
    }

    private static bool IsPointOnSegment(GeoCoordinate p, GeoCoordinate q, GeoCoordinate r)
    {
        return q.Longitude <= Math.Max(p.Longitude, r.Longitude) + 1e-9 &&
               q.Longitude >= Math.Min(p.Longitude, r.Longitude) - 1e-9 &&
               q.Latitude <= Math.Max(p.Latitude, r.Latitude) + 1e-9 &&
               q.Latitude >= Math.Min(p.Latitude, r.Latitude) - 1e-9;
    }

    /// <summary>
    /// Calculates the enclosed geographic area in square meters (m²) using localized metric projection and Shoelace formula.
    /// Accurately reflects real-world ground area regardless of latitude.
    /// </summary>
    public static double CalculatePolygonAreaSquareMeters(IReadOnlyList<GeoCoordinate> points)
    {
        if (points == null || points.Count < 3)
            return 0.0;

        // Use the first vertex as the local tangent origin
        double originLat = points[0].Latitude;
        double originLon = points[0].Longitude;
        double originLatRad = originLat * Math.PI / 180.0;

        // Earth mean radius in meters (WGS84 approx)
        const double earthRadius = 6371000.0;
        double metersPerDegreeLat = (Math.PI / 180.0) * earthRadius;
        double metersPerDegreeLon = (Math.PI / 180.0) * earthRadius * Math.Cos(originLatRad);

        // Project each geographic point to Cartesian coordinates (meters)
        double sum = 0.0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;

            double xi = (points[i].Longitude - originLon) * metersPerDegreeLon;
            double yi = (points[i].Latitude - originLat) * metersPerDegreeLat;

            double xNext = (points[next].Longitude - originLon) * metersPerDegreeLon;
            double yNext = (points[next].Latitude - originLat) * metersPerDegreeLat;

            sum += (xi * yNext) - (xNext * yi);
        }

        return Math.Abs(sum) * 0.5;
    }

    /// <summary>
    /// Calculates the enclosed geographic area. Defaults to square meters (m²).
    /// </summary>
    public static double CalculatePolygonArea(IReadOnlyList<GeoCoordinate> points)
    {
        return CalculatePolygonAreaSquareMeters(points);
    }

    /// <summary>
    /// Simplifies polygon vertices using the closed-loop Ramer-Douglas-Peucker (RDP) algorithm with adaptive tolerance.
    /// Considers both the perimeter and the closing edge by splitting at the antipodal vertex.
    /// </summary>
    public static List<GeoCoordinate> SimplifyPolygon(List<GeoCoordinate> points, int targetMaxCount = 16)
    {
        var normalized = NormalizePolygonPoints(points);
        if (normalized.Count <= targetMaxCount)
            return new List<GeoCoordinate>(normalized);

        // Adaptive tolerance: start with ~0.00008 deg (~9m) and adjust if needed
        double epsilon = 0.00008;
        var simplified = SimplifyClosedPolygonRDP(normalized, epsilon);

        // If still too many points for comfortable touch pin manipulation, gradually increase epsilon
        int iterations = 0;
        while (simplified.Count > targetMaxCount && iterations < 5)
        {
            epsilon *= 1.5;
            simplified = SimplifyClosedPolygonRDP(normalized, epsilon);
            iterations++;
        }

        // Guarantee minimum 3 distinct vertices if original polygon had at least 3 distinct vertices
        var distinctSimplified = GetDistinctVertices(simplified);
        if (distinctSimplified.Count < 3 && normalized.Count >= 3)
        {
            // Pick evenly spaced points to maintain triangular/quad base
            return PickEvenlySpaced(normalized, Math.Min(targetMaxCount, Math.Max(3, normalized.Count)));
        }

        return distinctSimplified;
    }

    /// <summary>
    /// Simplifies a closed polygon by splitting it at the furthest antipodal point and running RDP on both halves.
    /// Eliminates arbitrary starting point bias and preserves contours around the closing boundary.
    /// </summary>
    public static List<GeoCoordinate> SimplifyClosedPolygonRDP(List<GeoCoordinate> closedVertices, double epsilon)
    {
        if (closedVertices == null || closedVertices.Count <= 3)
            return closedVertices != null ? new List<GeoCoordinate>(closedVertices) : new List<GeoCoordinate>();

        // 1. Find the vertex furthest from index 0 to serve as the antipodal split point
        int n = closedVertices.Count;
        int furthestIdx = 1;
        double maxDistSq = 0.0;
        var origin = closedVertices[0];

        for (int i = 1; i < n; i++)
        {
            double dLat = closedVertices[i].Latitude - origin.Latitude;
            double dLon = closedVertices[i].Longitude - origin.Longitude;
            double distSq = dLat * dLat + dLon * dLon;
            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
                furthestIdx = i;
            }
        }

        // Ensure furthestIdx is valid to split into two non-empty halves
        if (furthestIdx <= 0 || furthestIdx >= n - 1)
        {
            furthestIdx = n / 2;
        }

        // 2. Build Part 1: from 0 to furthestIdx
        var part1 = new List<GeoCoordinate>();
        for (int i = 0; i <= furthestIdx; i++)
        {
            part1.Add(closedVertices[i]);
        }

        // 3. Build Part 2: from furthestIdx to n-1 then back to 0
        var part2 = new List<GeoCoordinate>();
        for (int i = furthestIdx; i < n; i++)
        {
            part2.Add(closedVertices[i]);
        }
        part2.Add(closedVertices[0]); // complete the loop back to 0

        // 4. Simplify both halves independently with standard RDP
        var simplified1 = RamerDouglasPeucker(part1, epsilon);
        var simplified2 = RamerDouglasPeucker(part2, epsilon);

        // 5. Merge results:
        var combined = new List<GeoCoordinate>();
        for (int i = 0; i < simplified1.Count - 1; i++)
        {
            combined.Add(simplified1[i]);
        }
        for (int i = 0; i < simplified2.Count - 1; i++)
        {
            combined.Add(simplified2[i]);
        }

        return GetDistinctVertices(combined);
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
