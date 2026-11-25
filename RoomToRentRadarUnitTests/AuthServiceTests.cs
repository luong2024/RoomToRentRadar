using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.SavedSearches;

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
            DemoLoginResult = new LoginResponse("demo-token", expiresAt, "Demo Renter", AppUserRoleWire.Student)
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
            Assert.That(tokenStore.Role, Is.EqualTo(AppUserRole.Student));
        });
    }

    [Test]
    public async Task EnsureSignedIn_WithStoredRole_RestoresStoredRole()
    {
        var apiClient = new FakeApiClient();
        var tokenStore = new FakeTokenStore
        {
            AccessToken = "stored-token",
            Role = AppUserRole.Moderator
        };
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.True);
            Assert.That(service.CurrentRole, Is.EqualTo(AppUserRole.Moderator));
            Assert.That(apiClient.DemoLoginCount, Is.Zero);
        });
    }

    [Test]
    public async Task EnsureSignedIn_WithLegacyTokenWithoutRole_DefaultsToStudent()
    {
        var apiClient = new FakeApiClient();
        var tokenStore = new FakeTokenStore
        {
            AccessToken = "legacy-token",
            Role = null
        };
        var service = new AuthService(apiClient, tokenStore);

        var signedIn = await service.EnsureSignedInAsync();

        Assert.Multiple(() =>
        {
            Assert.That(signedIn, Is.True);
            Assert.That(service.CurrentRole, Is.EqualTo(AppUserRole.Student));
        });
    }

    [Test]
    public async Task BeginRegistration_ReturnsChallengeWithoutStartingSession()
    {
        var challengeId = Guid.NewGuid();
        var expiresAt = new DateTime(2026, 12, 2, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            BeginRegistrationResult = new RegisterChallengeResponse(
                challengeId,
                "landlord@example.test",
                expiresAt)
        };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);
        var sessionChanges = 0;
        service.SessionChanged += (_, _) => sessionChanges++;

        var challenge = await service.BeginRegistrationAsync(
            new RegisterRequest("landlord@example.test", "Password-123", "New Landlord", AppUserRoleWire.Landlord));

        Assert.Multiple(() =>
        {
            Assert.That(challenge.ChallengeId, Is.EqualTo(challengeId));
            Assert.That(challenge.ExpiresAt, Is.EqualTo(expiresAt));
            Assert.That(apiClient.RequestedRegistration?.Role, Is.EqualTo(AppUserRoleWire.Landlord));
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(service.CurrentRole, Is.Null);
            Assert.That(sessionChanges, Is.Zero);
        });
    }

    [Test]
    public async Task VerifyRegistration_SavesSessionRoleAndRaisesSessionChanged()
    {
        var challengeId = Guid.NewGuid();
        var expiresAt = new DateTime(2026, 12, 2, 8, 0, 0, DateTimeKind.Utc);
        var apiClient = new FakeApiClient
        {
            VerifyRegistrationResult = new LoginResponse(
                "register-token",
                expiresAt,
                "New Landlord",
                AppUserRoleWire.Landlord)
        };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);
        var sessionChanges = 0;
        service.SessionChanged += (_, _) => sessionChanges++;

        await service.VerifyRegistrationAsync(challengeId, "246810");

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.RequestedVerification?.ChallengeId, Is.EqualTo(challengeId));
            Assert.That(apiClient.RequestedVerification?.Code, Is.EqualTo("246810"));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("register-token"));
            Assert.That(tokenStore.ExpiresAt, Is.EqualTo(expiresAt));
            Assert.That(tokenStore.Role, Is.EqualTo(AppUserRole.Landlord));
            Assert.That(service.CurrentRole, Is.EqualTo(AppUserRole.Landlord));
            Assert.That(sessionChanges, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenApiRejectsCode_KeepsSessionEmpty()
    {
        var apiClient = new FakeApiClient
        {
            VerifyRegistrationException = new ApiException(
                HttpStatusCode.BadRequest,
                "Invalid code.",
                "invalid_otp",
                attemptsRemaining: 4)
        };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);

        Assert.ThrowsAsync<ApiException>(async () => await service.VerifyRegistrationAsync(
            Guid.NewGuid(),
            "000000"));

        Assert.Multiple(() =>
        {
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(service.CurrentRole, Is.Null);
        });
    }

    [Test]
    public async Task SignOut_ClearsRoleAndRaisesSessionChanged()
    {
        var apiClient = new FakeApiClient
        {
            DemoLoginResult = new LoginResponse("demo-token", DateTime.UtcNow.AddHours(1), "Demo Renter", AppUserRoleWire.Admin)
        };
        var tokenStore = new FakeTokenStore();
        var service = new AuthService(apiClient, tokenStore);
        await service.EnsureSignedInAsync();
        var sessionChanges = 0;
        service.SessionChanged += (_, _) => sessionChanges++;

        await service.SignOutAsync();

        Assert.Multiple(() =>
        {
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(tokenStore.Role, Is.Null);
            Assert.That(service.CurrentRole, Is.Null);
            Assert.That(sessionChanges, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdateCurrentDisplayName_TrimsAndRaisesSessionChangedOnlyWhenChanged()
    {
        var tokenStore = new FakeTokenStore { AccessToken = "stored-token", Role = AppUserRole.Student };
        var service = new AuthService(new FakeApiClient(), tokenStore);
        await service.EnsureSignedInAsync();
        var sessionChanges = 0;
        service.SessionChanged += (_, _) => sessionChanges++;

        service.UpdateCurrentDisplayName("  Tên Mới  ");
        service.UpdateCurrentDisplayName("Tên Mới");

        Assert.Multiple(() =>
        {
            Assert.That(service.CurrentDisplayName, Is.EqualTo("Tên Mới"));
            Assert.That(sessionChanges, Is.EqualTo(1));
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

        public RegisterChallengeResponse? BeginRegistrationResult { get; set; }

        public Exception? BeginRegistrationException { get; set; }

        public LoginResponse? LoginResult { get; set; }

        public LoginResponse? VerifyRegistrationResult { get; set; }

        public Exception? VerifyRegistrationException { get; set; }

        public RegisterRequest? RequestedRegistration { get; private set; }

        public VerifyRegistrationRequest? RequestedVerification { get; private set; }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
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
            => Task.FromResult(LoginResult);

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
        {
            DemoLoginCount++;

            return DemoLoginException is { } exception
                ? Task.FromException<LoginResponse?>(exception)
                : Task.FromResult(DemoLoginResult);
        }

        public Task<RegisterChallengeResponse> BeginRegistrationAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestedRegistration = request;

            if (BeginRegistrationException is { } exception)
            {
                return Task.FromException<RegisterChallengeResponse>(exception);
            }

            return BeginRegistrationResult is { } result
                ? Task.FromResult(result)
                : Task.FromException<RegisterChallengeResponse>(
                    new NotSupportedException("No registration challenge was configured."));
        }

        public Task<LoginResponse> VerifyRegistrationAsync(
            VerifyRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestedVerification = request;

            if (VerifyRegistrationException is { } exception)
            {
                return Task.FromException<LoginResponse>(exception);
            }

            return VerifyRegistrationResult is { } result
                ? Task.FromResult(result)
                : Task.FromException<LoginResponse>(new NotSupportedException("No verification result was configured."));
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not search by area.");

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not save searches.");

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not change alerts.");

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not delete saved searches.");

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Automatic sign-in does not load notifications.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; set; }

        public DateTime? ExpiresAt { get; private set; }

        public AppUserRole? Role { get; set; }

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
}
