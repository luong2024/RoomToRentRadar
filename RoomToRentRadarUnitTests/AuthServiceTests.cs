using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthServiceTests
{
    [Test]
    public async Task EnsureSignedIn_WithStoredToken_DoesNotCallDemoLogin()
    {
        var apiClient = new FakeApiClient();
        var tokenStore = new FakeTokenStore { AccessToken = "stored-token" };
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.True);
            Assert.That(apiClient.DemoLoginCount, Is.Zero);
            Assert.That(tokenStore.AccessToken, Is.EqualTo("stored-token"));
        });
    }

    [Test]
    public async Task EnsureSignedIn_WithoutToken_SavesDemoSession()
    {
        var expiresAt = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            DemoLoginResult = new LoginResponse("demo-token", expiresAt, "Demo Renter")
        };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.True);
            Assert.That(apiClient.DemoLoginCount, Is.EqualTo(1));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("demo-token"));
            Assert.That(tokenStore.ExpiresAt, Is.EqualTo(expiresAt));
        });
    }

    [Test]
    public async Task EnsureSignedIn_WhenDemoLoginUnavailable_ReturnsFalse()
    {
        var apiClient = new FakeApiClient { DemoLoginResult = null };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.False);
            Assert.That(tokenStore.AccessToken, Is.Null);
        });
    }

    [Test]
    public async Task EnsureSignedIn_WhenApiThrows_ReturnsFalseWithoutThrowing()
    {
        var apiClient = new FakeApiClient { DemoLoginException = new HttpRequestException("API offline") };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.False);
            Assert.That(tokenStore.AccessToken, Is.Null);
        });
    }

    private sealed class FakeApiClient : IApiClient
    {
        public int DemoLoginCount { get; private set; }

        public LoginResponse? DemoLoginResult { get; set; }

        public Exception? DemoLoginException { get; set; }

        public Task<PagedResult<ListingSummaryDto>> GetListingsAsync(
            int limit,
            string? cursor = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not load listings.");

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not load listing details.");

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not load listing contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not use explicit credentials.");

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
        {
            DemoLoginCount++;

            return DemoLoginException is { } exception
                ? Task.FromException<LoginResponse?>(exception)
                : Task.FromResult(DemoLoginResult);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not sign out.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; set; }

        public DateTime? ExpiresAt { get; private set; }

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
            return Task.CompletedTask;
        }
    }
}
