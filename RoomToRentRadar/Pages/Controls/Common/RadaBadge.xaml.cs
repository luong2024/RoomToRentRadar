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
        BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(RadaBadge), Colors.Transparent);

    public static readonly BindableProperty BadgeTextColorProperty =
        BindableProperty.Create(nameof(BadgeTextColor), typeof(Color), typeof(RadaBadge), Colors.Transparent);

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
        switch (Type)
        {
            case RadaBadgeType.Verified:
                SetDynamicResource(BadgeColorProperty, "ActionAccentLight");
                SetDynamicResource(BadgeTextColorProperty, "ActionPrimaryLight");
                if (string.IsNullOrEmpty(Text) || Text == "Verified" || Text == "Available" || Text == "Almost Full" || Text == "Full")
                    Text = "Verified";
                break;

            case RadaBadgeType.Available:
                SetDynamicResource(BadgeColorProperty, "SurfaceSubtleLight");
                SetDynamicResource(BadgeTextColorProperty, "StatusSuccessTextLight");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("xác thực") || Text.Contains("Full") || Text.Contains("hết"))
                    Text = "Available";
                break;

            case RadaBadgeType.AlmostFull:
                SetDynamicResource(BadgeColorProperty, "SurfaceSubtleLight");
                SetDynamicResource(BadgeTextColorProperty, "StatusWarningTextLight");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Full"))
                    Text = "Almost Full";
                break;

            case RadaBadgeType.Full:
                SetDynamicResource(BadgeColorProperty, "SurfaceSubtleLight");
                SetDynamicResource(BadgeTextColorProperty, "StatusDangerLight");
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Almost"))
                    Text = "Full";
                break;

            case RadaBadgeType.Custom:
                // Retain custom BadgeColor and BadgeTextColor
                break;
        }
    }
}
