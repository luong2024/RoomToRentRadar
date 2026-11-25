namespace RoomToRentRadar.Views.MapSearch;

internal readonly record struct MapCanvasTransform(float Density, int OffsetX, int OffsetY)
{
    public (int X, int Y) ToMapPixels(PointF point) =>
        ((int)MathF.Round(point.X * Density + OffsetX),
         (int)MathF.Round(point.Y * Density + OffsetY));

    public PointF ToCanvasPoint(int x, int y) =>
        new((x - OffsetX) / Density, (y - OffsetY) / Density);
}
