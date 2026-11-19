using System.Text;
using System.Text.Json;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RouteGuardServiceTests
{
    private RouteGuardService _guardService = null!;

    [SetUp]
    public void Setup()
    {
        _guardService = new RouteGuardService();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(AppRoutes.Home)]
    [TestCase("//" + AppRoutes.Home)]
    [TestCase(AppRoutes.RoomListings)]
    [TestCase("//" + AppRoutes.RoomListings)]
    [TestCase(AppRoutes.RoomDetails)]
    [TestCase("rooms/details?roomId=123")]
    [TestCase("//rooms?search=ictu&priceMax=2000000")]
    [TestCase("..")]
    public void EvaluateRouteAccess_WhenRouteIsPublic_ShouldAllowForGuestAndAuthenticatedUser(string? route)
    {
        var guestResult = _guardService.EvaluateRouteAccess(route, null);
        Assert.That(guestResult, Is.EqualTo(RouteAccessStatus.Allowed));

        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        var studentResult = _guardService.EvaluateRouteAccess(route, student);
        Assert.That(studentResult, Is.EqualTo(RouteAccessStatus.Allowed));

        var landlord = new AuthUser(Guid.NewGuid(), "landlord@test.com", UserRoleConstants.Landlord, "LOCAL", true);
        var landlordResult = _guardService.EvaluateRouteAccess(route, landlord);
        Assert.That(landlordResult, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [TestCase(AppRoutes.Login)]
    [TestCase("//" + AppRoutes.Login)]
    [TestCase("login")]
    [TestCase("//login")]
    [TestCase("auth")]
    [TestCase("AUTH/LOGIN")]
    [TestCase("register")]
    [TestCase("//auth/register")]
    public void EvaluateRouteAccess_WhenGuestAccessesLoginOrRegister_ShouldAllow(string route)
    {
        var result = _guardService.EvaluateRouteAccess(route, null);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [TestCase(AppRoutes.Login)]
    [TestCase("//" + AppRoutes.Login)]
    [TestCase("login")]
    [TestCase("//login")]
    [TestCase("auth/login")]
    [TestCase("register")]
    [TestCase("//auth/register")]
    public void EvaluateRouteAccess_WhenAuthenticatedUserAccessesLoginOrRegister_ShouldRedirectGuestOnlyToHome(string route)
    {
        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(route, student);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.RedirectGuestOnlyToHome));
    }

    [TestCase(AppRoutes.Profile)]
    [TestCase("//" + AppRoutes.Profile)]
    [TestCase(AppRoutes.Booking)]
    [TestCase("//" + AppRoutes.Booking)]
    [TestCase(AppRoutes.RoommateMatching)]
    [TestCase("//" + AppRoutes.RoommateMatching)]
    public void EvaluateRouteAccess_WhenGuestAccessesProtectedRoute_ShouldRequireAuthentication(string route)
    {
        var result = _guardService.EvaluateRouteAccess(route, null);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.RequiresAuthentication));
    }

    [Test]
    public void EvaluateRouteAccess_WhenStudentAccessesRoommates_ShouldAllow()
    {
        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(AppRoutes.RoommateMatching, student);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [Test]
    public void EvaluateRouteAccess_WhenAdminAccessesRoommates_ShouldAllow()
    {
        var admin = new AuthUser(Guid.NewGuid(), "admin@test.com", "ADMIN", "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(AppRoutes.RoommateMatching, admin);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [Test]
    public void EvaluateRouteAccess_WhenLandlordAccessesRoommates_ShouldDenyWithRoleMismatch()
    {
        var landlord = new AuthUser(Guid.NewGuid(), "landlord@test.com", UserRoleConstants.Landlord, "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(AppRoutes.RoommateMatching, landlord);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.AccessDeniedRoleMismatch));
    }

    [TestCase(UserRoleConstants.Student)]
    [TestCase(UserRoleConstants.Landlord)]
    [TestCase("ADMIN")]
    public void EvaluateRouteAccess_WhenAnyRoleAccessesProfile_ShouldAllow(string role)
    {
        var user = new AuthUser(Guid.NewGuid(), "user@test.com", role, "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(AppRoutes.Profile, user);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [TestCase(UserRoleConstants.Student)]
    [TestCase(UserRoleConstants.Landlord)]
    [TestCase("ADMIN")]
    public void EvaluateRouteAccess_WhenAnyRoleAccessesBooking_ShouldAllow(string role)
    {
        var user = new AuthUser(Guid.NewGuid(), "user@test.com", role, "LOCAL", true);
        var result = _guardService.EvaluateRouteAccess(AppRoutes.Booking, user);
        Assert.That(result, Is.EqualTo(RouteAccessStatus.Allowed));
    }

    [TestCase("//rooms/details?id=99#photos", "rooms/details")]
    [TestCase("///booking///", "booking")]
    [TestCase("AUTH/LOGIN?returnUrl=%2Fprofile", "auth/login")]
    public void NormalizeRoute_ShouldStripSlashesQueryParamsAndFragments(string input, string expected)
    {
        var actual = RouteGuardService.NormalizeRoute(input);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task InterceptNavigationAsync_WhenSessionHydratesValidUser_ShouldWaitAndAllowProtectedRoute()
    {
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);
        var guard = new RouteGuardService(sessionService);

        var student = new AuthUser(Guid.NewGuid(), "hydrated@test.com", UserRoleConstants.Student, "LOCAL", true);
        var validJwt = CreateValidJwt();
        await storage.SetAsync(AuthenticationSessionService.AccessTokenKey, validJwt);
        await storage.SetAsync(AuthenticationSessionService.RefreshTokenKey, "refresh-token");
        await storage.SetAsync(AuthenticationSessionService.UserDataKey, JsonSerializer.Serialize(student));

        // Simulate app reload on a protected route while session restoration is still running
        var restoreTask = Task.Run(async () =>
        {
            await Task.Delay(60);
            return await sessionService.TryRestoreSessionAsync();
        });

        var decision = await guard.InterceptNavigationAsync("//profile");
        await restoreTask;

        Assert.Multiple(() =>
        {
            Assert.That(sessionService.IsInitialized, Is.True);
            Assert.That(decision.IsAllowed, Is.True);
            Assert.That(decision.Status, Is.EqualTo(RouteAccessStatus.Allowed));
            Assert.That(decision.RedirectRoute, Is.Null);
        });
    }

    [Test]
    public async Task InterceptNavigationAsync_WhenSessionHydratesGuest_ShouldRedirectToLoginWithPreservedReturnUrl()
    {
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);
        var guard = new RouteGuardService(sessionService);

        await sessionService.TryRestoreSessionAsync();

        var decision = await guard.InterceptNavigationAsync("//booking?roomId=42");

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Status, Is.EqualTo(RouteAccessStatus.RequiresAuthentication));
            Assert.That(decision.RedirectRoute, Is.EqualTo($"//{AppRoutes.Login}?returnUrl={Uri.EscapeDataString("//booking?roomId=42")}"));
        });
    }

    [Test]
    public async Task InterceptNavigationAsync_WhenAuthenticatedUserAccessesGuestRoute_ShouldRedirectToHome()
    {
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);
        var guard = new RouteGuardService(sessionService);

        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        await sessionService.SaveSessionAsync(CreateValidJwt(), "refresh-token", student);

        var decision = await guard.InterceptNavigationAsync("//auth/login");

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Status, Is.EqualTo(RouteAccessStatus.RedirectGuestOnlyToHome));
            Assert.That(decision.RedirectRoute, Is.EqualTo($"//{AppRoutes.Home}"));
        });
    }

    [Test]
    public async Task InterceptNavigationAsync_WhenLandlordAccessesRoommates_ShouldRedirectToHomeWithAlertMessage()
    {
        var storage = new InMemoryStorage();
        var authPort = new StubAuthPort();
        var sessionService = new AuthenticationSessionService(storage, authPort);
        var guard = new RouteGuardService(sessionService);

        var landlord = new AuthUser(Guid.NewGuid(), "landlord@test.com", UserRoleConstants.Landlord, "LOCAL", true);
        await sessionService.SaveSessionAsync(CreateValidJwt(), "refresh-token", landlord);

        var decision = await guard.InterceptNavigationAsync("//roommates");

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Status, Is.EqualTo(RouteAccessStatus.AccessDeniedRoleMismatch));
            Assert.That(decision.RedirectRoute, Is.EqualTo($"//{AppRoutes.Home}"));
            Assert.That(decision.AlertMessage, Is.EqualTo(RouteGuardService.RoleMismatchAlertMessage));
        });
    }

    [Test]
    public async Task InterceptNavigationAsync_WhenSessionInitializationThrows_ShouldFailClosedAndRedirectToHome()
    {
        var faultingSessionService = new FaultingSessionService();
        var guard = new RouteGuardService(faultingSessionService);

        var decision = await guard.InterceptNavigationAsync("//profile");

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.Status, Is.EqualTo(RouteAccessStatus.RequiresAuthentication));
            Assert.That(decision.RedirectRoute, Is.EqualTo($"//{AppRoutes.Home}"));
        });
    }

    [TestCase("//booking?roomId=42", "//booking?roomId=42")]
    [TestCase("profile", "//profile")]
    [TestCase("//roommates?campus=ictu", "//roommates?campus=ictu")]
    [TestCase("rooms/details?id=room-1", "rooms/details?id=room-1")]
    [TestCase("//booking?filter=a%2Fb&nested=%252F", "//booking?filter=a%2Fb&nested=%252F")]
    public void ResolvePostLoginRedirectRoute_WhenReturnUrlIsValidForUser_ShouldReturnFormattedTargetRouteWithoutDoubleDecoding(
        string returnUrl,
        string expectedRoute)
    {
        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        var actual = _guardService.ResolvePostLoginRedirectRoute(returnUrl, student);
        Assert.That(actual, Is.EqualTo(expectedRoute));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("//auth/login")]
    [TestCase("register")]
    [TestCase("https://evil.example.com/phishing")]
    [TestCase("javascript:alert(1)")]
    [TestCase("%2F%2Fauth%2Flogin")]
    public void ResolvePostLoginRedirectRoute_WhenReturnUrlIsMissingGuestOrExternal_ShouldFallbackToHome(string? returnUrl)
    {
        var student = new AuthUser(Guid.NewGuid(), "student@test.com", UserRoleConstants.Student, "LOCAL", true);
        var actual = _guardService.ResolvePostLoginRedirectRoute(returnUrl, student);
        Assert.That(actual, Is.EqualTo($"//{AppRoutes.Home}"));
    }

    [Test]
    public void ResolvePostLoginRedirectRoute_WhenReturnUrlViolatesUserRole_ShouldFallbackToHome()
    {
        var landlord = new AuthUser(Guid.NewGuid(), "landlord@test.com", UserRoleConstants.Landlord, "LOCAL", true);
        var actual = _guardService.ResolvePostLoginRedirectRoute("//roommates", landlord);
        Assert.That(actual, Is.EqualTo($"//{AppRoutes.Home}"));
    }

    private static string CreateValidJwt()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"sub":"123","exp":{{exp}}}"""));
        return $"{header}.{payload}.sig";
    }

    private sealed class FaultingSessionService : IAuthenticationSessionService
    {
        public bool IsAuthenticated => false;
        public bool IsInitialized => false;
        public AuthUser? CurrentUser => throw new InvalidOperationException("Session state corrupted");

#pragma warning disable CS0067
        public event EventHandler<AuthUser?>? CurrentUserChanged;
#pragma warning restore CS0067

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Failed to initialize session");

        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task ClearSessionAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class InMemoryStorage : ISecureStorageService
    {
        private readonly Dictionary<string, string> _dict = new();

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_dict.TryGetValue(key, out var val) ? val : null);

        public Task SetAsync(string key, string value)
        {
            _dict[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key) => _dict.Remove(key);

        public void RemoveAll() => _dict.Clear();
    }

    private sealed class StubAuthPort : IAuthenticationPort
    {
        public Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
