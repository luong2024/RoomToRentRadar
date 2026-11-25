using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class LoginViewModelTests
{
    [Test]
    public async Task SignIn_WhenCredentialsValid_SavesTokenAndNavigatesToRoleRoot()
    {
        var expiresAt = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            LoginResult = new LoginResponse("token-123", expiresAt, "Demo Tester", AppUserRoleWire.Student)
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Email = "  demo@example.test  ";
        viewModel.Password = "Demo-password-123";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.Empty);
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(apiClient.RequestedLogins, Has.Count.EqualTo(1));
            Assert.That(apiClient.RequestedLogins[0].Email, Is.EqualTo("demo@example.test"));
            Assert.That(apiClient.RequestedLogins[0].Password, Is.EqualTo("Demo-password-123"));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("token-123"));
            Assert.That(tokenStore.ExpiresAt, Is.EqualTo(expiresAt));
            Assert.That(tokenStore.SavedRole, Is.EqualTo(AppUserRole.Student));
            Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { AppUserRole.Student }));
            Assert.That(navigationService.GoBackCount, Is.Zero);
        });
    }

    [TestCase(AppUserRole.Student)]
    [TestCase(AppUserRole.Landlord)]
    [TestCase(AppUserRole.Moderator)]
    [TestCase(AppUserRole.Admin)]
    public async Task SignIn_RedirectsToTheRootOfTheSignedInRole(AppUserRole role)
    {
        var apiClient = new FakeApiClient
        {
            LoginResult = new LoginResponse("token-123", DateTime.UtcNow.AddDays(7), "Demo Tester", AppUserRoleWire.ToWireValue(role))
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Email = "demo@example.test";
        viewModel.Password = "Demo-password-123";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { role }));
    }

    [Test]
    public async Task GoToRegister_NavigatesToTheRegistrationScreen()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), navigationService);
        viewModel.SelectLandlordCommand.Execute(null);

        await viewModel.GoToRegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(navigationService.GoToRegisterCount, Is.EqualTo(1));
            Assert.That(navigationService.RequestedRegisterRole, Is.EqualTo(AppUserRole.Landlord));
        });
    }

    [Test]
    public async Task SignIn_WhenApiRejectsCredentials_ShowsErrorAndDoesNotNavigate()
    {
        var apiClient = new FakeApiClient { LoginResult = null };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Email = "demo@example.test";
        viewModel.Password = "wrong-password";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Email hoặc mật khẩu không đúng."));
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigationService.GoBackCount, Is.Zero);
        });
    }

    [TestCase("", "Demo-password-123")]
    [TestCase("demo@example.test", "")]
    [TestCase("   ", "   ")]
    public async Task SignIn_WhenFieldsMissing_ShowsValidationErrorAndDoesNotCallApi(string email, string password)
    {
        var apiClient = new FakeApiClient
        {
            LoginResult = new LoginResponse("token-123", DateTime.UtcNow.AddDays(7), "Demo Tester", AppUserRoleWire.Student)
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.Email = email;
        viewModel.Password = password;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Nhập email và mật khẩu."));
            Assert.That(apiClient.RequestedLogins, Is.Empty);
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigationService.GoBackCount, Is.Zero);
        });
    }

    [Test]
    public async Task SignIn_WhenEmailFormatInvalid_ShowsInlineEmailErrorWithoutCallingApi()
    {
        var apiClient = new FakeApiClient();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.Email = "not-an-email";
        viewModel.Password = "Demo-password-123";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasEmailError, Is.True);
            Assert.That(viewModel.EmailError, Is.EqualTo("Email không hợp lệ."));
            Assert.That(viewModel.HasPasswordError, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Email không hợp lệ."));
            Assert.That(apiClient.RequestedLogins, Is.Empty);
        });

        viewModel.Email = "demo@example.test";
        Assert.That(viewModel.HasEmailError, Is.False);
    }

    [Test]
    public async Task SignIn_WhenRateLimited_ShowsFriendlyMessageWithoutErrorHandler()
    {
        var apiClient = new FakeApiClient
        {
            LoginException = new ApiException(
                System.Net.HttpStatusCode.TooManyRequests,
                "Too many requests.",
                "rate_limit_exceeded")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService(), errorHandler);
        viewModel.Email = "demo@example.test";
        viewModel.Password = "Demo-password-123";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("quá nhiều lần"));
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task SignIn_WhenApiThrows_ShowsErrorAndReportsException()
    {
        var exception = new HttpRequestException("network down");
        var apiClient = new FakeApiClient { LoginException = exception };
        var errorHandler = new FakeErrorHandler();
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService, errorHandler);
        viewModel.Email = "demo@example.test";
        viewModel.Password = "Demo-password-123";

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể đăng nhập. Kiểm tra kết nối và thử lại."));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigationService.GoBackCount, Is.Zero);
        });
    }

    [Test]
    public async Task Cancel_WhenInvoked_NavigatesBack()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), navigationService);

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoBackCount, Is.EqualTo(1));
    }

    private static LoginViewModel CreateViewModel(
        FakeApiClient apiClient,
        FakeTokenStore tokenStore,
        FakeNavigationService navigationService,
        FakeErrorHandler? errorHandler = null)
        => new(
            new AuthService(apiClient, tokenStore),
            errorHandler ?? new FakeErrorHandler(),
            navigationService);

    private sealed class FakeApiClient : IApiClient
    {
        public List<LoginRequest> RequestedLogins { get; } = new();

        public LoginResponse? LoginResult { get; set; }

        public Exception? LoginException { get; set; }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not load listings.");

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not load listing details.");

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not load listing contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            RequestedLogins.Add(request);

            return LoginException is { } exception
                ? Task.FromException<LoginResponse?>(exception)
                : Task.FromResult(LoginResult);
        }

        public Task<RegisterChallengeResponse> BeginRegistrationAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen registers through the registration screen.");

        public Task<LoginResponse> VerifyRegistrationAsync(
            VerifyRegistrationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen registers through the registration screen.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen signs in with explicit credentials.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not search by area.");

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not save searches.");

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not change alerts.");

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not delete saved searches.");

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not load notifications.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; private set; }

        public DateTime? ExpiresAt { get; private set; }

        public AppUserRole? SavedRole { get; private set; }

        public int ClearCount { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(SavedRole);

        public Task SaveAsync(
            string accessToken,
            DateTime expiresAt,
            AppUserRole role,
            CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            SavedRole = role;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            ExpiresAt = null;
            SavedRole = null;
            ClearCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex)
        {
            HandledExceptions.Add(ex);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;

        public int GoBackCount { get; private set; }

        public int GoToLoginCount { get; private set; }

        public int GoToRegisterCount { get; private set; }

        public AppUserRole? RequestedRegisterRole { get; private set; }

        public List<AppUserRole?> RoleRootRoles { get; } = new();

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The login screen does not open room details.");

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToRegisterAsync()
        {
            GoToRegisterCount++;
            return Task.CompletedTask;
        }

        public Task GoToRegisterAsync(AppUserRole? initialRole)
        {
            GoToRegisterCount++;
            RequestedRegisterRole = initialRole;
            return Task.CompletedTask;
        }

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The login screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The login screen does not open room comparison.");

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }
    }
}
