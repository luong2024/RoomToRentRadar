using Android.Gms.Maps.Model;
using Android.Graphics;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps.Handlers;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using Path = Android.Graphics.Path;

namespace RoomToRentRadar.Views.MapSearch;

internal sealed class RoomPricePin(string priceTag) : Pin
{
    public string PriceTag { get; } = priceTag;
}

internal sealed class RoomPricePinHandler : MapPinHandler
{
    protected override MarkerOptions CreatePlatformElement()
    {
        var marker = base.CreatePlatformElement();
        if (VirtualView is not RoomPricePin pin)
            return marker;

        float density = MauiContext?.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        using var paint = new Paint(PaintFlags.AntiAlias)
        {
            TextSize = 13 * density,
            TextAlign = Paint.Align.Center
        };
        paint.SetTypeface(Typeface.DefaultBold);
        int width = (int)MathF.Ceiling(paint.MeasureText(pin.PriceTag) + 22 * density);
        int height = (int)MathF.Ceiling(35 * density);
        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        float center = width / 2f;
        float bodyBottom = 28 * density;
        float tip = height - density;
        using var tail = new Path();
        tail.MoveTo(center - 5 * density, bodyBottom - density);
        tail.LineTo(center, tip);
        tail.LineTo(center + 5 * density, bodyBottom - density);
        tail.Close();

        paint.Color = Color.ParseColor("#93C5FD");
        canvas.DrawRoundRect(density, density, width - density, bodyBottom, 8 * density, 8 * density, paint);
        canvas.DrawPath(tail, paint);
        paint.Color = Color.White;
        canvas.DrawRoundRect(2 * density, 2 * density, width - 2 * density, bodyBottom - density,
            7 * density, 7 * density, paint);
        paint.Color = Color.ParseColor("#1E3A8A");
        float baseline = (density + bodyBottom - paint.Ascent() - paint.Descent()) / 2;
        canvas.DrawText(pin.PriceTag, center, baseline, paint);

        using var icon = BitmapDescriptorFactory.FromBitmap(bitmap);
        marker.SetIcon(icon);
        marker.Anchor(0.5f, tip / height);
        return marker;
    }
}
