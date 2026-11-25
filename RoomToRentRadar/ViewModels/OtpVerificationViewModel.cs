using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.ViewModels;

public partial class OtpVerificationViewModel : ObservableObject, IQueryAttributable
{
    private readonly AuthService _authService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private Guid _challengeId;

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    public partial string Code { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCodeError))]
    public partial string CodeError { get; set; }

    public OtpVerificationViewModel(
        AuthService authService,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _authService = authService;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        Email = string.Empty;
        Code = string.Empty;
        ErrorMessage = string.Empty;
        CodeError = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasCodeError => !string.IsNullOrEmpty(CodeError);

    public string CodeHint => $"Chúng tôi đã gửi mã xác nhận gồm {AccountRules.OtpLength} chữ số tới:";

    public string LifetimeHint => $"Mã có hiệu lực trong {AccountRules.OtpLifetimeMinutes} phút.";

    public int CodeMaxLength => AccountRules.OtpLength;

    partial void OnCodeChanged(string value) => CodeError = string.Empty;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        ClearErrors();

        if (_challengeId == Guid.Empty)
        {
            ErrorMessage = "Yêu cầu xác nhận không hợp lệ. Hãy quay lại và đăng ký lại.";
            return;
        }

        var code = Code.Trim();
        if (!AccountRules.IsOtpCodeValid(code))
        {
            CodeError = $"Nhập mã xác nhận gồm {AccountRules.OtpLength} chữ số từ email.";
            ErrorMessage = CodeError;
            return;
        }

        IsBusy = true;
        try
        {
            await _authService.VerifyRegistrationAsync(_challengeId, code, cancellationToken);
            await _navigationService.GoToRoleRootAsync(_authService.CurrentRole);
        }
        catch (ApiException ex) when (ex.Code == "invalid_otp")
        {
            CodeError = ex.AttemptsRemaining is { } remaining
                ? $"Mã xác nhận không đúng. Bạn còn {remaining} lần thử."
                : "Mã xác nhận không đúng. Kiểm tra lại mã trong email.";
            ErrorMessage = CodeError;
        }
        catch (ApiException ex) when (ex.Code == "invalid_otp_challenge")
        {
            ErrorMessage = "Mã xác nhận đã hết hiệu lực. Hãy quay lại và đăng ký lại để nhận mã mới.";
        }
        catch (ApiException ex) when (ex.Code == "otp_attempts_exceeded")
        {
            ErrorMessage = "Bạn đã nhập sai quá số lần cho phép. Hãy quay lại và đăng ký lại để nhận mã mới.";
        }
        catch (ApiException ex) when (ex.Code == "email_already_registered")
        {
            ErrorMessage = "Email này đã được đăng ký. Hãy quay lại và đăng nhập.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ErrorMessage = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = "Mã xác nhận chưa hợp lệ. Kiểm tra lại và thử lại.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể xác nhận. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => _navigationService.GoBackAsync();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("challenge_id", out var rawChallengeId))
        {
            _challengeId = rawChallengeId switch
            {
                Guid id => id,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty
            };
        }

        if (query.TryGetValue("email", out var rawEmail) && rawEmail is string email)
        {
            Email = email;
        }
    }

    private void ClearErrors()
    {
        ErrorMessage = string.Empty;
        CodeError = string.Empty;
    }
}
