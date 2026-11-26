using Microsoft.Maui.Devices;

namespace RoomToRentRadar.Helpers;

public static class MotionEffects
{
    public static bool IsReducedMotionEnabled()
    {
#if ANDROID
        try
        {
            var resolver = Android.App.Application.Context?.ContentResolver;
            if (resolver is not null)
            {
                var scale = Android.Provider.Settings.Global.GetFloat(
                    resolver,
                    Android.Provider.Settings.Global.AnimatorDurationScale,
                    1.0f);
                return scale <= 0.01f;
            }
        }
        catch
        {
        }
#endif
        return false;
    }

    public static async Task FadeSlideInAsync(VisualElement? element, double offsetY = 12, uint durationMs = 240)
    {
        if (element is null)
            return;

        if (IsReducedMotionEnabled())
        {
            element.Opacity = 1;
            element.TranslationY = 0;
            element.Scale = 1;
            return;
        }

        element.Opacity = 0;
        element.TranslationY = offsetY;
        element.Scale = 0.985;
        await Task.WhenAll(
            element.FadeToAsync(1, durationMs, Easing.CubicOut),
            element.TranslateToAsync(0, 0, durationMs, Easing.CubicOut),
            element.ScaleToAsync(1, durationMs, Easing.CubicOut));
    }

    public static async Task PulsePopAsync(VisualElement? element)
    {
        if (element is null || IsReducedMotionEnabled())
            return;

        await element.ScaleToAsync(0.90, 80, Easing.CubicOut);
        await element.ScaleToAsync(1.06, 120, Easing.CubicOut);
        await element.ScaleToAsync(1.0, 110, Easing.SpringOut);
    }

    public static async Task LiquidPressAsync(VisualElement? element)
    {
        if (element is null || IsReducedMotionEnabled())
            return;

        await element.ScaleToAsync(0.95, 70, Easing.CubicOut);
        await element.ScaleToAsync(1.0, 140, Easing.SpringOut);
    }

    public static void TryLightHaptic()
    {
        try
        {
            if (HapticFeedback.Default.IsSupported)
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
        }
    }
}
