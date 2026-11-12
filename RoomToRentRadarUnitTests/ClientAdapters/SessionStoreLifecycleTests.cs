using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class SessionStoreLifecycleTests
{
    [Test]
    public async Task SaveSessionAsync_ValidTokens_StoresTokensAndReturnsTrue()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var tokens = new AuthTokensDto(
            AccessToken: "access-token-123",
            RefreshToken: "refresh-token-456",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "u1",
                Email: "student@ictu.edu.vn",
                FullName: "Student",
                Role: "Student",
                AuthProvider: "Local",
                PhoneNumber: null,
                StudentCode: "DTC01",
                IsVerified: true,
                CreatedAtUtc: DateTime.UtcNow
            )
        );

        // Act
        var result = await store.SaveSessionAsync(tokens);
        var accessToken = await store.GetAccessTokenAsync();
        var refreshToken = await store.GetRefreshTokenAsync();
        var user = await store.GetCachedUserProfileAsync();
        var isValid = await store.HasValidTokenAsync();

        // Assert
        Assert.That(result, Is.True);
        Assert.That(accessToken, Is.EqualTo("access-token-123"));
        Assert.That(refreshToken, Is.EqualTo("refresh-token-456"));
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("student@ictu.edu.vn"));
        Assert.That(isValid, Is.True);
    }

    [Test]
    public async Task ClearSessionAsync_ClearsBothAccessTokenAndRefreshToken()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var tokens = new AuthTokensDto(
            AccessToken: "access-token-123",
            RefreshToken: "refresh-token-456",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: null!
        );

        await store.SaveSessionAsync(tokens);

        // Act
        await store.ClearSessionAsync();

        // Assert
        var accessToken = await store.GetAccessTokenAsync();
        var refreshToken = await store.GetRefreshTokenAsync();
        var isValid = await store.HasValidTokenAsync();

        Assert.That(accessToken, Is.Null);
        Assert.That(refreshToken, Is.Null);
        Assert.That(isValid, Is.False);
    }

    [Test]
    public async Task HasValidTokenAsync_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var store = new InMemorySessionStore();
        var tokens = new AuthTokensDto(
            AccessToken: "expired-access",
            RefreshToken: "expired-refresh",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(-5),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1),
            User: null!
        );

        await store.SaveSessionAsync(tokens);

        // Act
        var isValid = await store.HasValidTokenAsync();

        // Assert
        Assert.That(isValid, Is.False);
    }
}
