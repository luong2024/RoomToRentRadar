using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Profile.Models;
using RoomToRentRadar.Features.Profile.Services;
using RoomToRentRadar.Features.Profile.ViewModels;
using RoomToRentRadar.Infrastructure.Api;

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

    [Test]
    public async Task UpdateAsync_WhenPendingAvatarBelongsToPreviousUser_DoesNotSendItForCurrentUser()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var pendingAvatar = new PendingProfileAvatar();
        pendingAvatar.Set(userA, "data:image/png;base64,YXZhdGFyLWZvci11c2VyLWE=");
        var client = new CapturingApiClient();
        var service = new ProfileService(client);
        var profile = new ProfileApiDto { Role = "STUDENT", FullName = "User B" };

        await service.UpdateAsync(profile, pendingAvatar.GetFor(userB));

        Assert.Multiple(() =>
        {
            Assert.That(((UpdateTenantProfileRequest)client.PutRequest!).AvatarUrl, Is.Null);
            Assert.That(pendingAvatar.GetFor(userA), Is.Null);
        });
    }

    [Test]
    public async Task CurrentUserChanged_WhenProfilePageIsShowingAnotherAccount_ClearsOldProfileThenLoadsNewAccount()
    {
        var userA = new AuthUser(Guid.NewGuid(), "a@example.com", "STUDENT", "LOCAL", true);
        var userB = new AuthUser(Guid.NewGuid(), "b@example.com", "STUDENT", "LOCAL", true);
        var session = new FakeSession(userA);
        var profiles = new SwitchingProfileService(
            new ProfileApiDto { UserId = userA.Id, Role = "STUDENT", FullName = "User A" });
        var viewModel = new ProfileViewModel(profiles, new NoOpErrorHandler(), session);

        await viewModel.LoadAsync();
        Assert.That(viewModel.FullName, Is.EqualTo("User A"));

        await session.SaveSessionAsync("access", "refresh", userB);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.FullName, Is.Empty, "Old profile data must disappear before the new request completes.");
            Assert.That(viewModel.AccountEmail, Is.EqualTo("b@example.com"), "Only the current session identity may be shown while reload is pending.");
            Assert.That(viewModel.AvatarUrl, Is.Null);
        });

        profiles.CompleteNewProfile(new ProfileApiDto { UserId = userB.Id, Role = "STUDENT", FullName = "User B" });

        for (var attempt = 0; attempt < 20 && viewModel.FullName != "User B"; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.FullName, Is.EqualTo("User B"));
            Assert.That(viewModel.AccountEmail, Is.EqualTo("b@example.com"));
        });
    }

    private sealed class CapturingApiClient : IApiClient
    {
        public object? PutRequest { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult<TResponse?>(default);

        public Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
        {
            PutRequest = request;
            return Task.FromResult<TResponse?>(default);
        }
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

    private sealed class NoOpErrorHandler : IErrorHandler
    {
        public void HandleError(Exception ex) { }
    }

    private sealed class SwitchingProfileService(ProfileApiDto firstProfile) : IProfileService
    {
        private readonly TaskCompletionSource<ProfileApiDto?> _nextProfile = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _getCallCount;
        public Task<ProfileApiDto?> GetMyProfileAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _getCallCount) == 1)
            {
                return Task.FromResult<ProfileApiDto?>(firstProfile);
            }

            return _nextProfile.Task;
        }

        public async Task<ProfileApiDto?> UpdateAsync(ProfileApiDto profile, string? newAvatarData, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return profile;
        }

        public void CompleteNewProfile(ProfileApiDto profile)
        {
            _nextProfile.SetResult(profile);
        }
    }
}
