using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Views;
using AndroidX.Core.View;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using AColor = Android.Graphics.Color;
using AView = Android.Views.View;

namespace RoomToRentRadar.Platforms.Android;

public sealed class LiquidGlassShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new LiquidGlassBottomNavAppearanceTracker(this, shellItem);
}

internal sealed class LiquidGlassBottomNavAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    private BottomNavigationView? _bottomView;

    public LiquidGlassBottomNavAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
        : base(shellContext, shellItem)
    {
        App.ThemeChanged += OnAppThemeChanged;
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        _bottomView = bottomView;
        ApplyLiquidGlassDock(bottomView);
    }

    public override void ResetAppearance(BottomNavigationView bottomView)
    {
        base.ResetAppearance(bottomView);
        _bottomView = bottomView;
        ApplyLiquidGlassDock(bottomView);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            App.ThemeChanged -= OnAppThemeChanged;
            _bottomView = null;
        }

        base.Dispose(disposing);
    }

    private void OnAppThemeChanged(object? sender, EventArgs e)
    {
        var view = _bottomView;
        if (view is null || !view.IsAttachedToWindow)
            return;

        view.Post(() => ApplyLiquidGlassDock(view));
    }

    private static void ApplyLiquidGlassDock(BottomNavigationView bottomView)
    {
        var context = bottomView.Context;
        if (context?.Resources?.DisplayMetrics is null)
            return;

        var density = context.Resources.DisplayMetrics.Density;
        int Dp(float value) => (int)Math.Round(value * density);

        var isDark = App.IsDarkTheme;

        // 1. Floating detached full-pill geometry cleanly cleared above the Android gesture bar.
        var dockHeight = Dp(66);
        var sideMargin = Dp(18);
        var bottomMargin = Dp(30);
        var overlapTopMargin = -(dockHeight + bottomMargin);

        if (bottomView.LayoutParameters is ViewGroup.MarginLayoutParams marginParams)
        {
            marginParams.Height = dockHeight;
            marginParams.LeftMargin = sideMargin;
            marginParams.RightMargin = sideMargin;
            marginParams.BottomMargin = bottomMargin;
            marginParams.TopMargin = overlapTopMargin;
            bottomView.LayoutParameters = marginParams;
        }

        if (bottomView.Parent is ViewGroup parent)
        {
            parent.SetClipChildren(false);
            parent.SetClipToPadding(false);
        }

        // Suppress Material BottomNavigationView's internal system-bar bottom inset padding
        // so the full 66dp pill height holds icon + label with clean separation and zero overlap.
        ViewCompat.SetOnApplyWindowInsetsListener(bottomView, new ZeroInsetListener(Dp(6)));
        bottomView.SetPadding(Dp(6), 0, Dp(6), 0);
        bottomView.ItemIconSize = Dp(22);
        bottomView.ItemPaddingTop = Dp(10);
        bottomView.ItemPaddingBottom = Dp(10);
        bottomView.Elevation = Dp(16);
        bottomView.TranslationZ = Dp(16);
        bottomView.LabelVisibilityMode = NavigationBarView.LabelVisibilityLabeled;
        bottomView.ItemActiveIndicatorEnabled = false;

        // 2. Frosted Liquid Glass outer pill with high optical density (softens underlying text cleanly).
        var outerPill = new GradientDrawable();
        outerPill.SetShape(ShapeType.Rectangle);
        outerPill.SetCornerRadius(Dp(33));
        outerPill.SetColor(AColor.ParseColor(isDark ? "#EB10192A" : "#F4F7FCFF"));
        outerPill.SetStroke(
            Math.Max(1, Dp(1.25f)),
            AColor.ParseColor(isDark ? "#52FFFFFF" : "#99A6C8EB"));

        bottomView.Background = outerPill;
        bottomView.OutlineProvider = ViewOutlineProvider.Background;
        bottomView.ClipToOutline = true;

        // 3. Active & inactive icon/label tints (iOS 26 Liquid Glass Azure / Sapphire palette).
        var states = new[]
        {
            new[] { global::Android.Resource.Attribute.StateChecked },
            Array.Empty<int>()
        };
        var activeColor = AColor.ParseColor(isDark ? "#38BDF8" : "#0066FF");
        var inactiveColor = AColor.ParseColor(isDark ? "#E6EFFA" : "#3E5470");
        var tintList = new ColorStateList(states, new int[] { activeColor, inactiveColor });

        bottomView.ItemIconTintList = tintList;
        bottomView.ItemTextColor = tintList;
        bottomView.ItemRippleColor = ColorStateList.ValueOf(
            AColor.ParseColor(isDark ? "#2038BDF8" : "#180066FF"));

        // 4. Nested Liquid Glass pill highlight wrapping both icon & label for the active tab.
        ApplyActiveTabCapsule(bottomView, isDark, Dp);
        bottomView.Post(() => ApplyActiveTabCapsule(bottomView, isDark, Dp));
    }

    private static void ApplyActiveTabCapsule(BottomNavigationView bottomView, bool isDark, Func<float, int> dp)
    {
        if (bottomView.ChildCount == 0 || bottomView.GetChildAt(0) is not ViewGroup menuView)
            return;

        for (var i = 0; i < menuView.ChildCount; i++)
        {
            var itemView = menuView.GetChildAt(i);
            if (itemView is null)
                continue;

            var checkedPill = new GradientDrawable();
            checkedPill.SetShape(ShapeType.Rectangle);
            checkedPill.SetCornerRadius(dp(27));
            checkedPill.SetColor(AColor.ParseColor(isDark ? "#29FFFFFF" : "#1C0066FF"));
            checkedPill.SetStroke(
                Math.Max(1, dp(1f)),
                AColor.ParseColor(isDark ? "#3DFFFFFF" : "#380066FF"));

            var insetChecked = new InsetDrawable(checkedPill, dp(3), dp(5), dp(3), dp(5));

            var selector = new StateListDrawable();
            selector.AddState(new[] { global::Android.Resource.Attribute.StateChecked }, insetChecked);
            selector.AddState(Array.Empty<int>(), new ColorDrawable(AColor.Transparent));

            itemView.Background = selector;
        }
    }

    private sealed class ZeroInsetListener(int horizontalPadding) : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(AView? v, WindowInsetsCompat? insets)
        {
            v?.SetPadding(horizontalPadding, 0, horizontalPadding, 0);
            return insets;
        }
    }
}
