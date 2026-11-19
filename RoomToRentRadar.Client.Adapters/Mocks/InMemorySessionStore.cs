using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Client.Adapters.Mocks;

/// <summary>
/// In-memory session store for testing and preview modes, implementing ADR-005.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiresAtUtc;
    private string? _refreshToken;
    private DateTimeOffset? _refreshTokenExpiresAtUtc;
    private UserProfileDto? _cachedUser;

    public bool SimulateSaveFailure { get; set; } = false;

    public Task<bool> SaveSessionAsync(AuthTokensDto tokens, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        if (SimulateSaveFailure)
            return Task.FromResult(false);

        _accessToken = tokens.AccessToken;
        _accessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc;
        _refreshToken = tokens.RefreshToken;
        _refreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;
        _cachedUser = tokens.User;

        return Task.FromResult(true);
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_accessToken);
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_refreshToken);
    }

    public Task<UserProfileDto?> GetCachedUserProfileAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_cachedUser);
    }

    public Task<bool> HasValidTokenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAtUtc.HasValue)
        {
            if (_accessTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow)
                return Task.FromResult(true);
        }

        if (!string.IsNullOrWhiteSpace(_refreshToken) && _refreshTokenExpiresAtUtc.HasValue)
        {
            return Task.FromResult(_refreshTokenExpiresAtUtc.Value > DateTimeOffset.UtcNow);
        }

        return Task.FromResult(false);
    }

    public Task ClearSessionAsync(CancellationToken ct = default)
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = null;
        _refreshToken = null;
        _refreshTokenExpiresAtUtc = null;
        _cachedUser = null;

        return Task.CompletedTask;
    }
}

