using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Profile.Models;
using RoomToRentRadar.Features.Profile.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ProfileSessionSafetyTests
{
    [Test]
    public void CanApplyProfile_WhenSessionChangedAfterRequest_ReturnsFalse()
    {
        var originalUser = new AuthUser(Guid.NewGuid(), "a@example.com", "STUDENT", "LOCAL", true);
        var nextUser = new AuthUser(Guid.NewGuid(), "b@example.com", "STUDENT", "LOCAL", true);
        var session = new FakeSession(nextUser);
        var profile = new ProfileApiDto { UserId = originalUser.Id, Role = "STUDENT" };

        var canApply = ProfileSessionGuard.CanApplyProfile(session, originalUser, profile);

        Assert.That(canApply, Is.False);
    }

    [Test]
    public void TryDecode_WhenPersistedAvatarDataUri_ReturnsOriginalBytes()
    {
        var original = new byte[] { 1, 2, 3, 4 };
        var dataUri = $"data:image/png;base64,{Convert.ToBase64String(original)}";

        var decoded = AvatarDataUri.TryDecode(dataUri, out var bytes);

        Assert.Multiple(() =>
        {
            Assert.That(decoded, Is.True);
            Assert.That(bytes, Is.EqualTo(original));
        });
    }

    private sealed class FakeSession(AuthUser? user) : IAuthenticationSessionService
    {
        public bool IsAuthenticated => CurrentUser is not null;
        public AuthUser? CurrentUser { get; private set; } = user;
        public event EventHandler<AuthUser?>? CurrentUserChanged;
        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
        {
            CurrentUser = user;
            CurrentUserChanged?.Invoke(this, user);
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsAuthenticated);
        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            CurrentUser = null;
            CurrentUserChanged?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}
