using RoomToRentRadar.Features.Authentication.ViewModels;

namespace RoomToRentRadar.Features.Authentication.Views;

public partial class AuthPage : ContentPage
{
    private const string CardFlipAnimationName = "AuthCardFlip";
    private const string FocusAnimationName = "InputFocusGlow";

    private bool _isFlipping;
    private readonly Color _primaryFocusColor = Color.FromArgb("#2563EB"); // Modern Royal Blue
    private readonly Color _glowHighlightColor = Color.FromArgb("#60A5FA");
    private readonly Color _glowSoftColor = Color.FromArgb("#BFDBFE");
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
        StopAllFocusAnimations();
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
        StopAllFocusAnimations();

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

        var strokeBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(_glowSoftColor, 0.0f),
                new GradientStop(_primaryFocusColor, 0.35f),
                new GradientStop(_glowHighlightColor, 0.5f),
                new GradientStop(_primaryFocusColor, 0.65f),
                new GradientStop(_glowSoftColor, 1.0f)
            }
        };

        border.Stroke = strokeBrush;
        border.BackgroundColor = _focusedBackground;

        var glowAnimation = new Animation(v =>
        {
            var angle = v * 2.0 * Math.PI;
            var dx = 0.5 * Math.Cos(angle);
            var dy = 0.5 * Math.Sin(angle);
            strokeBrush.StartPoint = new Point(0.5 + dx, 0.5 + dy);
            strokeBrush.EndPoint = new Point(0.5 - dx, 0.5 - dy);
        }, 0.0, 1.0);

        glowAnimation.Commit(
            border,
            FocusAnimationName,
            rate: 16,
            length: 1800,
            easing: Easing.Linear,
            repeat: () => true);
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

        border.Stroke = new SolidColorBrush(_defaultBorderColor);
        border.BackgroundColor = _defaultBackground;
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
