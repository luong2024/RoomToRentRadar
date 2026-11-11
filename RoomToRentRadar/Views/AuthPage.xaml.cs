using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class AuthPage : ContentPage
{
    private bool _isFlipping;
    private readonly Color _primaryColor = Color.FromArgb("#512BD4");
    private readonly Color _gradientAccentColor = Color.FromArgb("#D600AA");

    public AuthPage(AuthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnToggleCardClicked(object? sender, EventArgs e)
    {
        if (_isFlipping)
        {
            return;
        }

        _isFlipping = true;

        try
        {
            // Phase 1: Rotate to edge-on (90 deg)
            await MainAuthCard.RotateYToAsync(90, 220, Easing.CubicIn);

            // Phase 2: Toggle ViewModel state between Login and Sign Up
            if (BindingContext is AuthViewModel vm)
            {
                vm.ToggleForm();
            }

            // Phase 3: Set position to opposite edge (-90 deg)
            MainAuthCard.RotationY = -90;

            // Phase 4: Rotate back to facing user (0 deg)
            await MainAuthCard.RotateYToAsync(0, 220, Easing.CubicOut);
        }
        finally
        {
            _isFlipping = false;
        }
    }

    private void OnInputFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;

        var (border, label) = GetBorderAndLabelForEntry(entry);
        if (border == null) return;

        // Highlight label
        if (label != null)
        {
            label.TextColor = _primaryColor;
        }

        // Apply animated focus border with LinearGradientBrush
        StartAnimatedFocusBorder(border);
    }

    private void OnInputUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;

        var (border, label) = GetBorderAndLabelForEntry(entry);
        if (border == null) return;

        // Restore label color
        if (label != null)
        {
            label.SetAppThemeColor(
                Label.TextColorProperty,
                (Color)Application.Current!.Resources["Gray900"],
                (Color)Application.Current!.Resources["Gray100"]);
        }

        // Stop focus animation and reset border stroke
        StopAnimatedFocusBorder(border);
    }

    private void StartAnimatedFocusBorder(Border border)
    {
        border.AbortAnimation("BorderFocusAnimation");

        var animation = new Animation(v =>
        {
            var startX = 0.5 + 0.5 * Math.Cos(v);
            var startY = 0.5 + 0.5 * Math.Sin(v);
            var endX = 0.5 - 0.5 * Math.Cos(v);
            var endY = 0.5 - 0.5 * Math.Sin(v);

            border.Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(startX, startY),
                EndPoint = new Point(endX, endY),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(_primaryColor, 0.0f),
                    new GradientStop(_gradientAccentColor, 1.0f)
                }
            };
        }, 0, 2 * Math.PI);

        animation.Commit(
            border,
            "BorderFocusAnimation",
            length: 2500,
            repeat: () => border.IsFocused || IsAnyChildFocused(border));
    }

    private static void StopAnimatedFocusBorder(Border border)
    {
        border.AbortAnimation("BorderFocusAnimation");

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var defaultBorderColor = isDark
            ? (Color)Application.Current!.Resources["Gray600"]
            : (Color)Application.Current!.Resources["Gray300"];

        border.Stroke = new SolidColorBrush(defaultBorderColor);
    }

    private static bool IsAnyChildFocused(Border border)
    {
        if (border.Content is VisualElement visual && visual.IsFocused)
        {
            return true;
        }
        return false;
    }

    private (Border? border, Label? label) GetBorderAndLabelForEntry(Entry entry)
    {
        if (entry == LoginEmailEntry) return (LoginEmailBorder, LoginEmailLabel);
        if (entry == LoginPasswordEntry) return (LoginPasswordBorder, LoginPasswordLabel);
        if (entry == SignUpEmailEntry) return (SignUpEmailBorder, SignUpEmailLabel);
        if (entry == SignUpPasswordEntry) return (SignUpPasswordBorder, SignUpPasswordLabel);
        if (entry == SignUpConfirmPasswordEntry) return (SignUpConfirmPasswordBorder, SignUpConfirmPasswordLabel);

        return (null, null);
    }
}
