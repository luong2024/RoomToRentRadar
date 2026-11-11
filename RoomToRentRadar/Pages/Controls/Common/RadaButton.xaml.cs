using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Common;

public enum RadaButtonVariant
{
    Primary,
    Secondary,
    Accent,
    Outline,
    Ghost,
    Destructive
}

public partial class RadaButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(RadaButton), string.Empty);

    public static readonly BindableProperty LeadingIconProperty =
        BindableProperty.Create(nameof(LeadingIcon), typeof(ImageSource), typeof(RadaButton), null,
            propertyChanged: (b, o, n) => ((RadaButton)b).UpdateIconVisibility());

    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(RadaButtonVariant), typeof(RadaButton), RadaButtonVariant.Primary,
            propertyChanged: (b, o, n) => ((RadaButton)b).ApplyVariantStyle());

    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(RadaButton), false,
            propertyChanged: (b, o, n) => ((RadaButton)b).UpdateLoadingState());

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(RadaButton), null);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(RadaButton), null);

    public static readonly BindableProperty ButtonHeightProperty =
        BindableProperty.Create(nameof(ButtonHeight), typeof(double), typeof(RadaButton), 44.0);

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(RadaButton), Colors.White);

    public static readonly BindableProperty HasLeadingIconProperty =
        BindableProperty.Create(nameof(HasLeadingIcon), typeof(bool), typeof(RadaButton), false);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ImageSource? LeadingIcon
    {
        get => (ImageSource?)GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    public RadaButtonVariant Variant
    {
        get => (RadaButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => (object?)GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public bool HasLeadingIcon
    {
        get => (bool)GetValue(HasLeadingIconProperty);
        private set => SetValue(HasLeadingIconProperty, value);
    }

    public RadaButton()
    {
        InitializeComponent();
        ApplyVariantStyle();
        UpdateIconVisibility();
    }

    private void UpdateIconVisibility()
    {
        HasLeadingIcon = LeadingIcon != null && !IsLoading;
    }

    private void UpdateLoadingState()
    {
        UpdateIconVisibility();
        ButtonBorder.Opacity = IsLoading ? 0.8 : 1.0;
    }

    private void ApplyVariantStyle()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources == null) return;

        var resources = Microsoft.Maui.Controls.Application.Current.Resources;

        switch (Variant)
        {
            case RadaButtonVariant.Primary:
                ButtonBorder.BackgroundColor = resources.TryGetValue("ActionPrimaryLight", out var pBg) ? (Color)pBg : Color.FromArgb("#163A5F");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;

            case RadaButtonVariant.Secondary:
                ButtonBorder.BackgroundColor = resources.TryGetValue("ActionSecondaryLight", out var sBg) ? (Color)sBg : Color.FromArgb("#256D85");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;

            case RadaButtonVariant.Accent:
                ButtonBorder.BackgroundColor = resources.TryGetValue("ActionAccentLight", out var aBg) ? (Color)aBg : Color.FromArgb("#4CB5AE");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;

            case RadaButtonVariant.Outline:
                ButtonBorder.BackgroundColor = Colors.Transparent;
                ButtonBorder.Stroke = resources.TryGetValue("BorderSubtleLight", out var oStroke) ? (Color)oStroke : Color.FromArgb("#E2E8F0");
                TextColor = resources.TryGetValue("TextPrimaryLight", out var oText) ? (Color)oText : Color.FromArgb("#1E293B");
                break;

            case RadaButtonVariant.Ghost:
                ButtonBorder.BackgroundColor = Colors.Transparent;
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = resources.TryGetValue("TextBrandLight", out var gText) ? (Color)gText : Color.FromArgb("#163A5F");
                break;

            case RadaButtonVariant.Destructive:
                ButtonBorder.BackgroundColor = resources.TryGetValue("StatusDangerLight", out var dBg) ? (Color)dBg : Color.FromArgb("#DC2626");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;
        }
    }
}
