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
        switch (Type)
        {
            case RadaBadgeType.Verified:
                BadgeColor = Color.FromArgb("#4CB5AE"); // Teal Accent
                BadgeTextColor = Color.FromArgb("#163A5F"); // Navy text (4.73:1 contrast WCAG AA)
                if (string.IsNullOrEmpty(Text) || Text == "✓ Verified" || Text == "✓ Đã xác thực" || Text == "Available" || Text == "Almost Full" || Text == "Full")
                    Text = "✓ Verified";
                break;

            case RadaBadgeType.Available:
                BadgeColor = Color.FromArgb("#DCFCE7"); // Soft green
                BadgeTextColor = Color.FromArgb("#15803D"); // Dark green text (5.0:1 contrast WCAG AA)
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("xác thực") || Text.Contains("Full") || Text.Contains("hết"))
                    Text = "Available";
                break;

            case RadaBadgeType.AlmostFull:
                BadgeColor = Color.FromArgb("#FEF3C7"); // Soft amber
                BadgeTextColor = Color.FromArgb("#B45309"); // Dark amber text (5.0:1 contrast WCAG AA)
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Full"))
                    Text = "Almost Full";
                break;

            case RadaBadgeType.Full:
                BadgeColor = Color.FromArgb("#FEE2E2"); // Soft red
                BadgeTextColor = Color.FromArgb("#B91C1C"); // Dark red text (5.1:1 contrast WCAG AA)
                if (string.IsNullOrEmpty(Text) || Text.Contains("Verified") || Text.Contains("Available") || Text.Contains("Almost"))
                    Text = "Full";
                break;

            case RadaBadgeType.Custom:
                // Retain custom BadgeColor and BadgeTextColor
                break;
        }
    }
}
