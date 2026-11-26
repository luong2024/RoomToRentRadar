using Microsoft.Maui.Graphics;

namespace RoomToRentRadar.Controls;

/// <summary>
/// Renders a silky, low-contrast atmospheric light field behind translucent Liquid Glass surfaces
/// so glass panels exhibit genuine optical depth and gentle color refraction without visual noise.
/// </summary>
public sealed class LiquidBackdropView : GraphicsView, IDrawable
{
    public LiquidBackdropView()
    {
        Drawable = this;
        InputTransparent = true;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        App.ThemeChanged += OnThemeChanged;
        if (Application.Current is { } app)
            app.RequestedThemeChanged += OnRequestedThemeChanged;
        Invalidate();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        App.ThemeChanged -= OnThemeChanged;
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnRequestedThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var w = dirtyRect.Width;
        var h = dirtyRect.Height;
        if (w <= 2 || h <= 2)
            return;

        var isDark = App.IsDarkTheme;
        canvas.SaveState();

        // 1. Silky Base Atmospheric Wash
        var basePaint = new LinearGradientPaint
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0.25, 1),
            GradientStops = isDark
                ? [new PaintGradientStop(0f, Color.FromArgb("#091224")), new PaintGradientStop(0.55f, Color.FromArgb("#080E1A")), new PaintGradientStop(1f, Color.FromArgb("#0B1424"))]
                : [new PaintGradientStop(0f, Color.FromArgb("#EEF5FF")), new PaintGradientStop(0.5f, Color.FromArgb("#F3F7FC")), new PaintGradientStop(1f, Color.FromArgb("#ECF3FB"))]
        };
        canvas.SetFillPaint(basePaint, dirtyRect);
        canvas.FillRectangle(dirtyRect);

        // 2. Top-Right Sapphire Sky Refraction Bloom
        DrawBloom(
            canvas,
            cx: w * 0.88f,
            cy: h * 0.08f,
            radius: Math.Max(w, h) * 0.48f,
            centerColor: isDark ? Color.FromArgb("#48104582") : Color.FromArgb("#66C2E0FF"),
            edgeColor: isDark ? Color.FromArgb("#00104582") : Color.FromArgb("#00C2E0FF"));

        // 3. Upper-Left Crystal Aqua Refraction Bloom
        DrawBloom(
            canvas,
            cx: w * 0.10f,
            cy: h * 0.24f,
            radius: Math.Max(w, h) * 0.42f,
            centerColor: isDark ? Color.FromArgb("#3A0B4D5C") : Color.FromArgb("#58C4F5EE"),
            edgeColor: isDark ? Color.FromArgb("#000B4D5C") : Color.FromArgb("#00C4F5EE"));

        // 4. Lower-Right Soft Iris Mist Bloom
        DrawBloom(
            canvas,
            cx: w * 0.82f,
            cy: h * 0.72f,
            radius: Math.Max(w, h) * 0.45f,
            centerColor: isDark ? Color.FromArgb("#301D265E") : Color.FromArgb("#45DCE2FF"),
            edgeColor: isDark ? Color.FromArgb("#001D265E") : Color.FromArgb("#00DCE2FF"));

        canvas.RestoreState();
    }

    private static void DrawBloom(ICanvas canvas, float cx, float cy, float radius, Color centerColor, Color edgeColor)
    {
        var bounds = new RectF(cx - radius, cy - radius, radius * 2f, radius * 2f);
        var radial = new RadialGradientPaint
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5,
            GradientStops =
            [
                new PaintGradientStop(0f, centerColor),
                new PaintGradientStop(1f, edgeColor)
            ]
        };
        canvas.SetFillPaint(radial, bounds);
        canvas.FillCircle(cx, cy, radius);
    }
}
