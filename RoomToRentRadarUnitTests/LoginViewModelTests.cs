using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Login.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class LoginViewModelTests
{
    [Test]
    public async Task SignIn_WhenCredentialsValid_SavesTokenAndNavigatesBack()
    {
        var expiresAt = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            LoginResult = new LoginResponse("token-123", expiresAt, "Demo Tester")
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
            Assert.That(navigationService.GoBackCount, Is.EqualTo(1));
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
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Email or password is incorrect."));
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
            LoginResult = new LoginResponse("token-123", DateTime.UtcNow.AddDays(7), "Demo Tester")
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
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Enter your email and password."));
            Assert.That(apiClient.RequestedLogins, Is.Empty);
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigationService.GoBackCount, Is.Zero);
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
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Could not sign in. Check your connection and try again."));
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

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen signs in with explicit credentials.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The login screen does not search by area.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; private set; }

        public DateTime? ExpiresAt { get; private set; }

        public int ClearCount { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task SaveAsync(string accessToken, DateTime expiresAt, CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            ExpiresAt = null;
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
        public int GoBackCount { get; private set; }

        public int GoToLoginCount { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The login screen does not open room details.");

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }
    }
}
