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
        BindableProperty.Create(nameof(Text), typeof(string), typeof(RadaBadge), "Đã xác thực",
            propertyChanged: (b, o, n) => ((RadaBadge)b).UpdateBadgeText());

    public static readonly BindableProperty TypeProperty =
        BindableProperty.Create(nameof(Type), typeof(RadaBadgeType), typeof(RadaBadge), RadaBadgeType.Verified,
            propertyChanged: (b, o, n) => ((RadaBadge)b).ApplyBadgeType());

    public static readonly BindableProperty BadgeColorProperty =
        BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(RadaBadge), Color.FromArgb("#4CB5AE"));

    public static readonly BindableProperty BadgeTextColorProperty =
        BindableProperty.Create(nameof(BadgeTextColor), typeof(Color), typeof(RadaBadge), Colors.White);

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

    private void UpdateBadgeText()
    {
        // Manual text override
    }

    private void ApplyBadgeType()
    {
        switch (Type)
        {
            case RadaBadgeType.Verified:
                BadgeColor = Color.FromArgb("#4CB5AE"); // Teal Accent
                BadgeTextColor = Colors.White;
                if (string.IsNullOrEmpty(Text) || Text == "Đã xác thực" || Text == "Còn phòng" || Text == "Sắp hết" || Text == "Hết phòng")
                    Text = "✓ Đã xác thực";
                break;

            case RadaBadgeType.Available:
                BadgeColor = Color.FromArgb("#16A34A"); // Green
                BadgeTextColor = Colors.White;
                if (string.IsNullOrEmpty(Text) || Text.Contains("xác thực") || Text.Contains("hết"))
                    Text = "Còn phòng";
                break;

            case RadaBadgeType.AlmostFull:
                BadgeColor = Color.FromArgb("#D97706"); // Amber
                BadgeTextColor = Colors.White;
                if (string.IsNullOrEmpty(Text) || Text.Contains("phòng") || Text.Contains("xác thực"))
                    Text = "Sắp hết";
                break;

            case RadaBadgeType.Full:
                BadgeColor = Color.FromArgb("#DC2626"); // Red
                BadgeTextColor = Colors.White;
                if (string.IsNullOrEmpty(Text) || Text.Contains("Còn") || Text.Contains("xác thực"))
                    Text = "Hết phòng";
                break;

            case RadaBadgeType.Custom:
                // Retain custom BadgeColor and BadgeTextColor
                break;
        }
    }
}

