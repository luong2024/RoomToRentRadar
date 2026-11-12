namespace RoomToRentRadar.Pages.Controls.Common;

public enum RadaBadgeType
{
    Verified,
    Available,
    AlmostFull,
    Full,
    Custom
}

public partial class RadaBadge : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(RadaBadge), string.Empty,
            propertyChanged: (b, o, n) => ((RadaBadge)b).OnTextChanged());

    public static readonly BindableProperty TypeProperty =
        BindableProperty.Create(nameof(Type), typeof(RadaBadgeType), typeof(RadaBadge), RadaBadgeType.Verified,
            propertyChanged: (b, o, n) => ((RadaBadge)b).ApplyBadgeType());

    public static readonly BindableProperty BadgeColorProperty =
        BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(RadaBadge), Color.FromArgb("#4CB5AE"));

    public static readonly BindableProperty BadgeTextColorProperty =
        BindableProperty.Create(nameof(BadgeTextColor), typeof(Color), typeof(RadaBadge), Color.FromArgb("#163A5F"));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public RadaBadgeType Type
    {
        get => (RadaBadgeType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public Color BadgeColor
    {
        get => (Color)GetValue(BadgeColorProperty);
        set => SetValue(BadgeColorProperty, value);
    }

    public Color BadgeTextColor
    {
        get => (Color)GetValue(BadgeTextColorProperty);
        set => SetValue(BadgeTextColorProperty, value);
    }

    public RadaBadge()
    {
        InitializeComponent();
        ApplyBadgeType();
    }

    private void OnTextChanged()
    {
        // Manual text override
    }

    private void ApplyBadgeType()
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;

        switch (Type)
        {
            case RadaBadgeType.Verified:
                BadgeColor = resources != null && resources.TryGetValue("ActionAccentLight", out var aBg) ? (Color)aBg : Color.FromArgb("#4CB5AE");
                BadgeTextColor = resources != null && resources.TryGetValue("ActionPrimaryLight", out var aTxt) ? (Color)aTxt : Color.FromArgb("#163A5F");
                if (string.IsNullOrEmpty(Text) || Text == "Verified" || Text == "Available" || Text == "Almost Full" || Text == "Full")
                    Text = "Verified";
                break;

            case RadaBadgeType.Available:
                BadgeColor = resources != null && resources.TryGetValue("SurfaceSubtleLight", out var avBg) ? (Color)avBg : Color.FromArgb("#DCFCE7");
                BadgeTextColor = resources != null && resources.TryGetValue("StatusSuccessTextLight", out var avTxt) ? (Color)avTxt : Color.FromArgb("#15803D");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("xác thực") || Text.Contains("Full") || Text.Contains("hết"))
                    Text = "Available";
                break;

            case RadaBadgeType.AlmostFull:
                BadgeColor = resources != null && resources.TryGetValue("SurfaceSubtleLight", out var afBg) ? (Color)afBg : Color.FromArgb("#FEF3C7");
                BadgeTextColor = resources != null && resources.TryGetValue("StatusWarningTextLight", out var afTxt) ? (Color)afTxt : Color.FromArgb("#B45309");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Full"))
                    Text = "Almost Full";
                break;

            case RadaBadgeType.Full:
                BadgeColor = resources != null && resources.TryGetValue("SurfaceSubtleLight", out var fBg) ? (Color)fBg : Color.FromArgb("#FEE2E2");
                BadgeTextColor = resources != null && resources.TryGetValue("StatusDangerLight", out var fTxt) ? (Color)fTxt : Color.FromArgb("#B91C1C");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Almost"))
                    Text = "Full";
                break;

            case RadaBadgeType.Custom:
                // Retain custom BadgeColor and BadgeTextColor
                break;
        }
    }
}
