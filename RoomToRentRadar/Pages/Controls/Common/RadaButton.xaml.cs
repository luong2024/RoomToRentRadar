using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

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

    public static readonly BindableProperty LeadingGlyphProperty =
        BindableProperty.Create(nameof(LeadingGlyph), typeof(string), typeof(RadaButton), string.Empty,
            propertyChanged: (b, o, n) => ((RadaButton)b).UpdateIconVisibility());

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(RadaButton), 8.0,
            propertyChanged: (b, o, n) => ((RadaButton)b).UpdateCornerRadius((double)n));

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

    public static readonly BindableProperty TextSizeProperty =
        BindableProperty.Create(nameof(TextSize), typeof(double), typeof(RadaButton), 14.0);

    public static readonly BindableProperty HasLeadingIconProperty =
        BindableProperty.Create(nameof(HasLeadingIcon), typeof(bool), typeof(RadaButton), false);

    public static readonly BindableProperty HasLeadingGlyphProperty =
        BindableProperty.Create(nameof(HasLeadingGlyph), typeof(bool), typeof(RadaButton), false);

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

    public string LeadingGlyph
    {
        get => (string)GetValue(LeadingGlyphProperty);
        set => SetValue(LeadingGlyphProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
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

    /// <summary>Label font size; defaults to <c>FontSizeBodyMedium</c>.</summary>
    public double TextSize
    {
        get => (double)GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public bool HasLeadingIcon
    {
        get => (bool)GetValue(HasLeadingIconProperty);
        private set => SetValue(HasLeadingIconProperty, value);
    }

    public bool HasLeadingGlyph
    {
        get => (bool)GetValue(HasLeadingGlyphProperty);
        private set => SetValue(HasLeadingGlyphProperty, value);
    }

    public RadaButton()
    {
        InitializeComponent();
        ApplyVariantStyle();
        UpdateIconVisibility();
        ButtonBorder.HandlerChanged += OnBorderHandlerChanged;
    }

    private void UpdateCornerRadius(double radius)
    {
        ButtonBorder.StrokeShape = new RoundRectangleGeometry { CornerRadius = new CornerRadius(radius) };
    }

    private void UpdateIconVisibility()
    {
        HasLeadingIcon = LeadingIcon != null && !IsLoading;
        HasLeadingGlyph = LeadingIcon == null && !string.IsNullOrEmpty(LeadingGlyph) && !IsLoading;
    }

    private void UpdateLoadingState()
    {
        UpdateIconVisibility();
        ButtonBorder.Opacity = IsLoading ? 0.8 : 1.0;
    }

    private void ApplyVariantStyle()
    {
        ButtonBorder.StrokeThickness = Variant == RadaButtonVariant.Outline ? 1.5 : 1.0;

        switch (Variant)
        {
            case RadaButtonVariant.Primary:
                ButtonBorder.SetDynamicResource(VisualElement.BackgroundColorProperty, "ActionPrimaryLight");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;

            case RadaButtonVariant.Secondary:
                ButtonBorder.SetDynamicResource(VisualElement.BackgroundColorProperty, "ActionSecondaryLight");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;

            case RadaButtonVariant.Accent:
                ButtonBorder.SetDynamicResource(VisualElement.BackgroundColorProperty, "ActionAccentLight");
                ButtonBorder.Stroke = Colors.Transparent;
                SetDynamicResource(TextColorProperty, "ActionPrimaryLight");
                break;

            case RadaButtonVariant.Outline:
                ButtonBorder.BackgroundColor = Colors.Transparent;
                ButtonBorder.SetDynamicResource(Border.StrokeProperty, "ActionPrimaryLight");
                SetDynamicResource(TextColorProperty, "ActionPrimaryLight");
                break;

            case RadaButtonVariant.Ghost:
                ButtonBorder.BackgroundColor = Colors.Transparent;
                ButtonBorder.Stroke = Colors.Transparent;
                SetDynamicResource(TextColorProperty, "TextBrandLight");
                break;

            case RadaButtonVariant.Destructive:
                ButtonBorder.SetDynamicResource(VisualElement.BackgroundColorProperty, "StatusDangerLight");
                ButtonBorder.Stroke = Colors.Transparent;
                TextColor = Colors.White;
                break;
        }
    }

    private void OnBorderHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (ButtonBorder.Handler?.PlatformView is Android.Views.View platformView)
        {
            platformView.SetAccessibilityDelegate(new ButtonAccessibilityDelegate());
        }
#endif
    }

#if ANDROID
    // A Border with a tap gesture is not announced as a button by TalkBack; expose the button role explicitly.
    private sealed class ButtonAccessibilityDelegate : Android.Views.View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(Android.Views.View host, Android.Views.Accessibility.AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info != null)
            {
                info.ClassName = "android.widget.Button";
                info.Clickable = true;
            }
        }
    }
#endif
}
