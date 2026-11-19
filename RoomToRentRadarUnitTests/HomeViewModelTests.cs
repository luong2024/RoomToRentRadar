using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HomeViewModelTests
{
    [Test]
    public async Task NavigateToAuthAsync_UsesTheUniqueAuthenticationShellRoute()
    {
        string? navigatedRoute = null;
        var viewModel = new HomeViewModel(route =>
        {
            navigatedRoute = route;
            return Task.CompletedTask;
        });

        await viewModel.NavigateToAuthAsync();

        Assert.That(navigatedRoute, Is.EqualTo("//auth/login"));
    }

    [Test]
    public void InitialState_WhenUnauthenticated_IsNotAuthenticatedIsTrueAndIsAuthenticatedIsFalse()
    {
        var session = new FakeSessionService(user: null);
        var viewModel = new HomeViewModel(navigateAsync: null, sessionService: session);

        Assert.That(viewModel.IsNotAuthenticated, Is.True);
        Assert.That(viewModel.IsAuthenticated, Is.False);
    }

    [Test]
    public void InitialState_WhenAuthenticated_IsNotAuthenticatedIsFalseAndIsAuthenticatedIsTrue()
    {
        var user = CreateTestUser();
        var session = new FakeSessionService(user);
        var viewModel = new HomeViewModel(navigateAsync: null, sessionService: session);

        Assert.That(viewModel.IsNotAuthenticated, Is.False);
        Assert.That(viewModel.IsAuthenticated, Is.True);
    }

    [Test]
    public async Task SessionState_WhenUserLogsIn_UpdatesAuthenticationProperties()
    {
        var session = new FakeSessionService(user: null);
        var viewModel = new HomeViewModel(navigateAsync: null, sessionService: session);

        Assert.That(viewModel.IsNotAuthenticated, Is.True);
        Assert.That(viewModel.IsAuthenticated, Is.False);

        var user = CreateTestUser();
        await session.SaveSessionAsync("token", "refresh", user);

        Assert.That(viewModel.IsNotAuthenticated, Is.False);
        Assert.That(viewModel.IsAuthenticated, Is.True);
    }

    [Test]
    public async Task SessionState_WhenUserLogsOut_RestoresUnauthenticatedProperties()
    {
        var user = CreateTestUser();
        var session = new FakeSessionService(user);
        var viewModel = new HomeViewModel(navigateAsync: null, sessionService: session);

        Assert.That(viewModel.IsNotAuthenticated, Is.False);
        Assert.That(viewModel.IsAuthenticated, Is.True);

        await session.LogoutAsync();

        Assert.That(viewModel.IsNotAuthenticated, Is.True);
        Assert.That(viewModel.IsAuthenticated, Is.False);
    }

    private static AuthUser CreateTestUser() => new(
        Id: Guid.NewGuid(),
        Email: "student@ictu.edu.vn",
        Role: "Student",
        Provider: "Local",
        IsActive: true);

    private sealed class FakeSessionService(AuthUser? user) : IAuthenticationSessionService
    {
        public bool IsAuthenticated => CurrentUser is not null;
        public bool IsInitialized => true;
        public AuthUser? CurrentUser { get; private set; } = user;
        public event EventHandler<AuthUser?>? CurrentUserChanged;

        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser authUser, CancellationToken cancellationToken = default)
        {
            CurrentUser = authUser;
            CurrentUserChanged?.Invoke(this, authUser);
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsAuthenticated);
        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public async Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            await ClearSessionAsync(cancellationToken);
            return new LogoutResult(LocalSessionCleared: true, ServerRevoked: true);
        }
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            CurrentUser = null;
            CurrentUserChanged?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}
