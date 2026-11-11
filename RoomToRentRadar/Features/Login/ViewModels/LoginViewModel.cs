using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Auth;

namespace RoomToRentRadar.Features.Login.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    public LoginViewModel(
        AuthService authService,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _authService = authService;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        Email = string.Empty;
        Password = string.Empty;
        ErrorMessage = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your email and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var signedIn = await _authService.SignInAsync(
                Email.Trim(),
                Password,
                cancellationToken);

            if (!signedIn)
            {
                ErrorMessage = "Email or password is incorrect.";
                return;
            }

            Password = string.Empty;
            await _navigationService.GoBackAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not sign in. Check your connection and try again.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigationService.GoBackAsync();
}
