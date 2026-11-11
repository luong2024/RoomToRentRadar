using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class AuthPage : ContentPage
{
    private bool _isFlipping;
    private readonly Color _primaryFocusColor = Color.FromArgb("#2563EB"); // Modern Royal Blue
    private readonly Color _defaultBorderColor = Color.FromArgb("#E2E8F0");
    private readonly Color _defaultLabelColor = Color.FromArgb("#334155");
    private readonly Color _focusedBackground = Color.FromArgb("#FFFFFF");
    private readonly Color _defaultBackground = Color.FromArgb("#F8FAFC");

    public AuthPage(AuthViewModel viewModel)
    {
        InitializeComponent();
        viewModel.NavigateToHomeHandler = async () =>
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//home");
            }
        };
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
            // Phase 1: 3D perspective rotation + scale depth + soft fade
            await Task.WhenAll(
                MainAuthCard.RotateYToAsync(90, 220, Easing.CubicIn),
                MainAuthCard.ScaleToAsync(0.92, 220, Easing.CubicIn),
                MainAuthCard.FadeToAsync(0.85, 220, Easing.CubicIn)
            );

            // Phase 2: Swap active face in ViewModel
            if (BindingContext is AuthViewModel vm)
            {
                vm.ToggleForm();
            }

            // Phase 3: Set transform to opposite angle
            MainAuthCard.RotationY = -90;

            // Phase 4: Rotate back to front + restore full scale & opacity
            await Task.WhenAll(
                MainAuthCard.RotateYToAsync(0, 250, Easing.CubicOut),
                MainAuthCard.ScaleToAsync(1.0, 250, Easing.CubicOut),
                MainAuthCard.FadeToAsync(1.0, 250, Easing.CubicOut)
            );
        }
        catch
        {
            MainAuthCard.RotationY = 0;
            MainAuthCard.Scale = 1.0;
            MainAuthCard.Opacity = 1.0;
        }
        finally
        {
            _isFlipping = false;
        }
    }

    private async void OnInputFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;

        var (border, label) = GetBorderAndLabelForEntry(entry);
        if (border == null) return;

        // Highlight label
        if (label != null)
        {
            label.TextColor = _primaryFocusColor;
        }

        // Apply focused border styling & subtle spring scale
        border.Stroke = new SolidColorBrush(_primaryFocusColor);
        border.StrokeThickness = 2.0;
        border.BackgroundColor = _focusedBackground;

        await border.ScaleToAsync(1.012, 140, Easing.CubicOut);
    }

    private async void OnInputUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;

        var (border, label) = GetBorderAndLabelForEntry(entry);
        if (border == null) return;

        // Restore default label color
        if (label != null)
        {
            label.TextColor = _defaultLabelColor;
        }

        // Restore default border styling
        border.Stroke = new SolidColorBrush(_defaultBorderColor);
        border.StrokeThickness = 1.5;
        border.BackgroundColor = _defaultBackground;

        await border.ScaleToAsync(1.0, 140, Easing.CubicIn);
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
