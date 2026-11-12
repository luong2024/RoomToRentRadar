using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class SecureStorageSessionStoreTests
{
    private ISecureStorageProvider _secureStorage = null!;
    private SecureStorageSessionStore _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _secureStorage = Substitute.For<ISecureStorageProvider>();
        _sut = new SecureStorageSessionStore(_secureStorage);
    }

    [Test]
    public async Task SaveSessionAsync_ValidTokens_StoresAccessTokenInMemoryAndRefreshTokenInSecureStorage()
    {
        // Arrange
        var tokens = new AuthTokensDto(
            AccessToken: "in-memory-access-token",
            RefreshToken: "persisted-refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "u-1",
                Email: "student@ictu.edu.vn",
                FullName: "Student Name",
                Role: "Student",
                AuthProvider: "Local",
                PhoneNumber: null,
                StudentCode: "DTC001",
                IsVerified: true,
                CreatedAtUtc: DateTime.UtcNow
            )
        );

        // Act
        var result = await _sut.SaveSessionAsync(tokens);
        var accessToken = await _sut.GetAccessTokenAsync();

        // Assert
        Assert.That(result, Is.True);
        Assert.That(accessToken, Is.EqualTo("in-memory-access-token"));

        // ADR-005 verification: Refresh token & profile written to secure storage, access token NOT written to secure storage
        await _secureStorage.Received(1).SetAsync(SecureStorageSessionStore.KeyRefreshToken, "persisted-refresh-token");
        await _secureStorage.Received(1).SetAsync(SecureStorageSessionStore.KeyUserProfile, Arg.Any<string>());
        await _secureStorage.DidNotReceive().SetAsync(Arg.Is<string>(k => k.Contains("access")), Arg.Any<string>());
    }

    [Test]
    public async Task SaveSessionAsync_WhenStorageThrowsException_ReturnsFalseSafely()
    {
        // Arrange
        _secureStorage.SetAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("Secure storage hardware failure"));

        var tokens = new AuthTokensDto(
            AccessToken: "access",
            RefreshToken: "refresh",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(1),
            User: null!
        );

        // Act
        var result = await _sut.SaveSessionAsync(tokens);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task GetRefreshTokenAsync_ReadsDirectlyFromSecureStorage()
    {
        // Arrange
        _secureStorage.GetAsync(SecureStorageSessionStore.KeyRefreshToken).Returns("stored-refresh-token");

        // Act
        var token = await _sut.GetRefreshTokenAsync();

        // Assert
        Assert.That(token, Is.EqualTo("stored-refresh-token"));
        await _secureStorage.Received(1).GetAsync(SecureStorageSessionStore.KeyRefreshToken);
    }

    [Test]
    public async Task GetCachedUserProfileAsync_WhenUserInStorage_DeserializesCorrectly()
    {
        // Arrange
        var userJson = "{\"Id\":\"u-42\",\"Email\":\"landlord@ictu.edu.vn\",\"FullName\":\"Test Owner\",\"Role\":\"Landlord\",\"AuthProvider\":\"Local\",\"IsVerified\":true,\"CreatedAtUtc\":\"2026-11-12T00:00:00Z\"}";
        _secureStorage.GetAsync(SecureStorageSessionStore.KeyUserProfile).Returns(userJson);

        // Act
        var profile = await _sut.GetCachedUserProfileAsync();

        // Assert
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.Email, Is.EqualTo("landlord@ictu.edu.vn"));
        Assert.That(profile.Role, Is.EqualTo("Landlord"));
    }

    [Test]
    public async Task ClearSessionAsync_ClearsMemoryAndCallsRemoveOnStorage()
    {
        // Arrange
        var tokens = new AuthTokensDto(
            AccessToken: "access-token",
            RefreshToken: "refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: null!
        );
        await _sut.SaveSessionAsync(tokens);

        // Act
        await _sut.ClearSessionAsync();
        var accessToken = await _sut.GetAccessTokenAsync();

        // Assert
        Assert.That(accessToken, Is.Null);
        _secureStorage.Received(1).Remove(SecureStorageSessionStore.KeyRefreshToken);
        _secureStorage.Received(1).Remove(SecureStorageSessionStore.KeyRefreshExpires);
        _secureStorage.Received(1).Remove(SecureStorageSessionStore.KeyUserProfile);
    }

    [Test]
    public async Task HasValidTokenAsync_WhenAccessTokenValid_ReturnsTrue()
    {
        // Arrange
        var tokens = new AuthTokensDto(
            AccessToken: "valid-access-token",
            RefreshToken: "refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: null!
        );
        await _sut.SaveSessionAsync(tokens);

        // Act
        var isValid = await _sut.HasValidTokenAsync();

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public async Task HasValidTokenAsync_WhenNoTokens_ReturnsFalse()
    {
        // Arrange
        _secureStorage.GetAsync(SecureStorageSessionStore.KeyRefreshToken).Returns((string?)null);

        // Act
        var isValid = await _sut.HasValidTokenAsync();

        // Assert
        Assert.That(isValid, Is.False);
    }
}
