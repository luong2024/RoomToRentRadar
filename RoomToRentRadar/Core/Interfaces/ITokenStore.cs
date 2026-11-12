namespace RoomToRentRadar.Core.Interfaces;

public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string accessToken, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
