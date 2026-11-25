using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class OtpVerificationViewModelTests
{
    private static readonly Guid ChallengeId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private const string Email = "student@example.test";

    [Test]
    public void ApplyQueryAttributes_WithStringAndGuidChallenge_ReadsBoth()
    {
        var viewModel = CreateViewModel(
            new FakeApiClient(),
            new FakeTokenStore(),
            new FakeNavigationService());
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Email, Is.EqualTo(Email));
            Assert.That(viewModel.Code, Is.Empty);
            Assert.That(viewModel.HasError, Is.False);
        });

        var guidViewModel = CreateViewModel(
            new FakeApiClient(),
            new FakeTokenStore(),
            new FakeNavigationService(),
            query: new Dictionary<string, object> { ["challenge_id"] = ChallengeId, ["email"] = Email });
        Assert.That(guidViewModel.Email, Is.EqualTo(Email));
    }

    [Test]
    public async Task Verify_WithValidCode_VerifiesAndRedirectsToRoleRoot()
    {
        var expiresAt = new DateTime(2026, 12, 2, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            VerifyResult = new LoginResponse("verify-token", expiresAt, "New Student", AppUserRoleWire.Student)
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Code = " 246810 ";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(apiClient.Verifications, Has.Count.EqualTo(1));
            Assert.That(apiClient.Verifications[0].ChallengeId, Is.EqualTo(ChallengeId));
            Assert.That(apiClient.Verifications[0].Code, Is.EqualTo("246810"));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("verify-token"));
            Assert.That(tokenStore.Role, Is.EqualTo(AppUserRole.Student));
            Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { AppUserRole.Student }));
        });
    }

    [Test]
    public async Task Verify_WithLandlordSession_RedirectsToLandlordRoot()
    {
        var apiClient = new FakeApiClient
        {
            VerifyResult = new LoginResponse(
                "verify-token",
                DateTime.UtcNow.AddDays(7),
                "New Landlord",
                AppUserRoleWire.Landlord)
        };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { AppUserRole.Landlord }));
    }

    [Test]
    public async Task Verify_WithMalformedCode_ShowsCodeErrorWithoutCallingApi()
    {
        var apiClient = new FakeApiClient();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.Code = "12345";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCodeError, Is.True);
            Assert.That(viewModel.CodeError, Does.Contain("6 chữ số"));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(apiClient.Verifications, Is.Empty);
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WithoutChallenge_ShowsInvalidRequestMessage()
    {
        var apiClient = new FakeApiClient();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(
            apiClient,
            new FakeTokenStore(),
            navigationService,
            query: new Dictionary<string, object>());
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("không hợp lệ"));
            Assert.That(apiClient.Verifications, Is.Empty);
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WhenCodeRejected_ShowsRemainingAttemptsAndKeepsSessionEmpty()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.BadRequest,
                "Invalid OTP.",
                "invalid_otp",
                attemptsRemaining: 3)
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Code = "000000";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CodeError, Does.Contain("còn 3 lần thử"));
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WhenChallengeExpired_ShowsExpiredMessage()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.BadRequest,
                "Verification code expired.",
                "invalid_otp_challenge")
        };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Does.Contain("hết hiệu lực"));
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WhenAttemptsExceeded_ShowsExceededMessage()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.TooManyRequests,
                "Too many attempts.",
                "otp_attempts_exceeded")
        };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.Code = "000000";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Does.Contain("quá số lần cho phép"));
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WhenEmailRegisteredMeanwhile_ShowsLoginMessage()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.Conflict,
                "Email already registered.",
                "email_already_registered")
        };
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.That(viewModel.ErrorMessage, Does.Contain("đăng nhập"));
    }

    [Test]
    public async Task Verify_WhenRateLimited_ShowsFriendlyMessageWithoutErrorHandler()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.TooManyRequests,
                "Too many requests.",
                "auth_rate_limited")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            apiClient,
            new FakeTokenStore(),
            new FakeNavigationService(),
            errorHandler);
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Does.Contain("quá nhanh"));
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task Verify_WhenApiThrows_ShowsConnectionErrorAndReportsException()
    {
        var exception = new HttpRequestException("network down");
        var apiClient = new FakeApiClient { VerifyException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            apiClient,
            new FakeTokenStore(),
            new FakeNavigationService(),
            errorHandler);
        viewModel.Code = "246810";

        await viewModel.VerifyCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể xác nhận. Kiểm tra kết nối và thử lại."));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public async Task Verify_WhenCodeEdited_ClearsCodeError()
    {
        var apiClient = new FakeApiClient
        {
            VerifyException = new ApiException(
                HttpStatusCode.BadRequest,
                "Invalid OTP.",
                "invalid_otp",
                attemptsRemaining: 2)
        };
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.Code = "000000";

        await viewModel.VerifyCommand.ExecuteAsync(null);
        Assert.That(viewModel.HasCodeError, Is.True);

        viewModel.Code = "000001";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasCodeError, Is.False);
            Assert.That(viewModel.HasError, Is.True);
        });
    }

    [Test]
    public async Task Back_WhenInvoked_NavigatesBack()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), navigationService);

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoBackCount, Is.EqualTo(1));
    }

    private static OtpVerificationViewModel CreateViewModel(
        FakeApiClient apiClient,
        FakeTokenStore tokenStore,
        FakeNavigationService navigationService,
        FakeErrorHandler? errorHandler = null,
        Dictionary<string, object>? query = null)
    {
        var viewModel = new OtpVerificationViewModel(
            new AuthService(apiClient, tokenStore),
            errorHandler ?? new FakeErrorHandler(),
            navigationService);
        viewModel.ApplyQueryAttributes(query ?? new Dictionary<string, object>
        {
            ["challenge_id"] = ChallengeId.ToString(),
            ["email"] = Email
        });
        return viewModel;
    }

    private sealed class FakeApiClient : IApiClient
    {
        public List<VerifyRegistrationRequest> Verifications { get; } = new();

        public LoginResponse? VerifyResult { get; set; }

        public Exception? VerifyException { get; set; }

        public Task<LoginResponse> VerifyRegistrationAsync(
            VerifyRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            Verifications.Add(request);

            return VerifyException is { } exception
                ? Task.FromException<LoginResponse>(exception)
                : Task.FromResult(VerifyResult!);
        }

        public Task<RegisterChallengeResponse> BeginRegistrationAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not start registrations.");

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not load listings.");

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not load listing details.");

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not load listing contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not sign in.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not use the demo login.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not search by area.");

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not save searches.");

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not change alerts.");

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not delete saved searches.");

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The OTP screen does not load notifications.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; private set; }

        public DateTime? ExpiresAt { get; private set; }

        public AppUserRole? Role { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Role);

        public Task SaveAsync(
            string accessToken,
            DateTime expiresAt,
            AppUserRole role,
            CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            Role = role;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            ExpiresAt = null;
            Role = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<AppUserRole?> RoleRootRoles { get; } = new();

        public int GoBackCount { get; private set; }

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The OTP screen does not open the listing editor.");

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The OTP screen does not open room details.");

        public Task GoToLoginAsync()
            => throw new NotSupportedException("The OTP screen does not open the login screen.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The OTP screen does not open the registration screen.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The OTP screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The OTP screen does not open room comparison.");
    }
}
