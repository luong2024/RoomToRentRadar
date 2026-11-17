using RoomToRentRadar.Features.Authentication.ViewModels;

namespace RoomToRentRadar.Features.Authentication.Views;

public partial class AuthPage : ContentPage
{
    private const string CardFlipAnimationName = "AuthCardFlip";
    private const string FocusAnimationName = "InputFocusLedGlow";

    private bool _isFlipping;
    private readonly Color _primaryFocusColor = Color.FromArgb("#2563EB"); // Modern Royal Blue
    private readonly Color _ledNeonCore = Color.FromArgb("#00F0FF");       // Vibrant Electric Cyan LED
    private readonly Color _ledElectricBlue = Color.FromArgb("#3B82F6");   // Royal Electric Blue
    private readonly Color _ledPurpleAura = Color.FromArgb("#8B5CF6");     // Neon Violet Aura
    private readonly Color _ledFadeColor = Color.FromArgb("#CBD5E1");      // Soft Falloff
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
        viewModel.OnAuthenticationSuccessHandler = async (user) =>
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//home");
            }
        };
        viewModel.FlipToLoginHandler = async () =>
        {
            await Task.Delay(700);
            if (viewModel.IsSignUpView)
            {
                await FlipCardAsync(viewModel);
            }
        };
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MainAuthCard.AbortAnimation(CardFlipAnimationName);
        UnfocusAllInputs();
    }

    private async void OnToggleCardClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not AuthViewModel vm)
        {
            return;
        }

        await FlipCardAsync(vm);
    }

    private async Task FlipCardAsync(AuthViewModel vm)
    {
        if (_isFlipping)
        {
            return;
        }

        _isFlipping = true;
        MainAuthCard.AbortAnimation(CardFlipAnimationName);
        UnfocusAllInputs();

        var startAngle = vm.IsSignUpView ? 180.0 : 0.0;
        var endAngle = vm.IsSignUpView ? 0.0 : 180.0;

        try
        {
            var tcs = new TaskCompletionSource<bool>();
            var swapped = false;

            var flipAnimation = new Animation(v =>
            {
                MainAuthCard.RotationY = v;
                if (!swapped && ((endAngle > startAngle && v >= 90.0) || (endAngle < startAngle && v <= 90.0)))
                {
                    swapped = true;
                    vm.ToggleForm();
                }
            }, startAngle, endAngle, Easing.CubicInOut);

            flipAnimation.Commit(
                MainAuthCard,
                CardFlipAnimationName,
                rate: 16,
                length: 500,
                finished: (_, canceled) =>
                {
                    if (!swapped)
                    {
                        vm.ToggleForm();
                    }
                    MainAuthCard.RotationY = endAngle;
                    tcs.TrySetResult(!canceled);
                });

            await tcs.Task;
        }
        catch
        {
            MainAuthCard.RotationY = vm.IsSignUpView ? 180.0 : 0.0;
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

        border.AbortAnimation(FocusAnimationName);

        if (label != null)
        {
            label.TextColor = _primaryFocusColor;
        }

        border.StrokeThickness = 2.0;
        border.BackgroundColor = _focusedBackground;

        // Continuous multi-color LED radial gradient simulating a traveling LED beacon along border edges
        var ledBrush = new RadialGradientBrush
        {
            Center = new Point(0, 0),
            Radius = 0.35,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(_ledNeonCore, 0.0f),      // Bright Neon Cyan Core
                new GradientStop(_ledElectricBlue, 0.22f), // Vivid Royal Blue Trail
                new GradientStop(_ledPurpleAura, 0.48f),   // Electric Violet Ambient Aura
                new GradientStop(_ledFadeColor, 0.78f),    // Soft Falloff
                new GradientStop(_defaultBorderColor, 1.0f)// Resting Border Tone
            }
        };

        border.Stroke = ledBrush;

        // Calculate and move LED bulb along the 4 edges in a closed loop (Top -> Right -> Bottom -> Left)
        var ledLoopAnimation = new Animation(v =>
        {
            ledBrush.Center = GetPerimeterPoint(v, border.Width, border.Height);
        }, 0.0, 1.0);

        ledLoopAnimation.Commit(
            border,
            FocusAnimationName,
            rate: 16,
            length: 2200,
            easing: Easing.Linear,
            repeat: () => true);
    }

    private static Point GetPerimeterPoint(double progress, double width, double height)
    {
        var w = width > 10 ? width : 320.0;
        var h = height > 10 ? height : 50.0;
        var totalPerimeter = 2.0 * (w + h);

        var topFrac = w / totalPerimeter;
        var rightFrac = h / totalPerimeter;
        var bottomFrac = w / totalPerimeter;
        var leftFrac = h / totalPerimeter;

        var p = progress % 1.0;
        if (p < 0) p += 1.0;

        if (p < topFrac)
        {
            // Top edge: (0, 0) -> (1, 0)
            var u = p / topFrac;
            return new Point(u, 0.0);
        }
        p -= topFrac;

        if (p < rightFrac)
        {
            // Right edge: (1, 0) -> (1, 1)
            var u = p / rightFrac;
            return new Point(1.0, u);
        }
        p -= rightFrac;

        if (p < bottomFrac)
        {
            // Bottom edge: (1, 1) -> (0, 1)
            var u = p / bottomFrac;
            return new Point(1.0 - u, 1.0);
        }
        p -= bottomFrac;

        // Left edge: (0, 1) -> (0, 0)
        var leftU = p / leftFrac;
        return new Point(0.0, 1.0 - leftU);
    }

    private void OnInputUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;

        var (border, label) = GetBorderAndLabelForEntry(entry);
        if (border == null) return;

        ResetBorderFocusState(border, label);
    }

    private void ResetBorderFocusState(Border border, Label? label)
    {
        border.AbortAnimation(FocusAnimationName);

        if (label != null)
        {
            label.TextColor = _defaultLabelColor;
        }

        border.StrokeThickness = 1.0;
        border.Stroke = new SolidColorBrush(_defaultBorderColor);
        border.BackgroundColor = _defaultBackground;
    }

    private void UnfocusAllInputs()
    {
        LoginEmailEntry.Unfocus();
        LoginPasswordEntry.Unfocus();
        SignUpEmailEntry.Unfocus();
        SignUpPasswordEntry.Unfocus();
        SignUpConfirmPasswordEntry.Unfocus();
        StopAllFocusAnimations();
    }

    private void StopAllFocusAnimations()
    {
        ResetBorderFocusState(LoginEmailBorder, LoginEmailLabel);
        ResetBorderFocusState(LoginPasswordBorder, LoginPasswordLabel);
        ResetBorderFocusState(SignUpEmailBorder, SignUpEmailLabel);
        ResetBorderFocusState(SignUpPasswordBorder, SignUpPasswordLabel);
        ResetBorderFocusState(SignUpConfirmPasswordBorder, SignUpConfirmPasswordLabel);
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
