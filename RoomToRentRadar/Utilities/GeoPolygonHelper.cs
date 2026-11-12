using System;
using System.Collections.Generic;

namespace RoomToRentRadar.Utilities;

/// <summary>
/// Xử lý quy đổi tọa độ cảm ứng sang GeoJSON [lng, lat] và kiểm tra điểm trong đa giác.
/// </summary>
public static class GeoPolygonHelper
{
    public static (double Latitude, double Longitude) ScreenPointToGeo(
        double x,
        double y,
        double viewWidth,
        double viewHeight,
        double centerLat,
        double centerLng,
        double latDegrees,
        double lngDegrees)
    {
        if (viewWidth <= 0 || viewHeight <= 0)
            return (centerLat, centerLng);

        double minLat = centerLat - (latDegrees / 2.0);
        double maxLat = centerLat + (latDegrees / 2.0);
        double minLng = centerLng - (lngDegrees / 2.0);

        double lat = Math.Clamp(maxLat - (y / viewHeight) * latDegrees, minLat, maxLat);
        double lng = minLng + (x / viewWidth) * lngDegrees;

        return (Math.Round(lat, 6), Math.Round(lng, 6));
    }

    /// <summary>
    /// Nhận danh sách (lat, lng) và tự động khép kín thành GeoJSON ring [[lng, lat], ..., [lng, lat]].
    /// </summary>
    public static List<double[]> BuildClosedGeoJsonRing(IReadOnlyList<(double Latitude, double Longitude)> vertices)
    {
        if (vertices.Count < 3)
            return [];

        var ring = new List<double[]>(vertices.Count + 1);
        foreach (var (lat, lng) in vertices)
        {
            ring.Add([Math.Round(lng, 6), Math.Round(lat, 6)]);
        }

        if (ring[0][0] != ring[^1][0] || ring[0][1] != ring[^1][1])
        {
            ring.Add([ring[0][0], ring[0][1]]);
        }

        return ring;
    }

    /// <summary>
    /// Thuật toán Ray-Casting kiểm tra tọa độ (lat, lng) có nằm trong polygon GeoJSON không.
    /// </summary>
    public static bool IsPointInPolygon(double lat, double lng, IReadOnlyList<double[]> closedGeoJsonRing)
    {
        if (closedGeoJsonRing.Count < 4)
            return false;

        var inside = false;
        int count = closedGeoJsonRing.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double xi = closedGeoJsonRing[i][0], yi = closedGeoJsonRing[i][1];
            double xj = closedGeoJsonRing[j][0], yj = closedGeoJsonRing[j][1];

            bool intersect = ((yi > lat) != (yj > lat))
                && (lng < (xj - xi) * (lat - yi) / ((yj - yi) + double.Epsilon) + xi);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}