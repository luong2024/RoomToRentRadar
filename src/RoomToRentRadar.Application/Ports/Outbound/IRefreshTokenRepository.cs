using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound persistence port for <see cref="RefreshToken"/> aggregates.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetByFamilyAsync(
        Guid tokenFamily,
        CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    void Update(RefreshToken refreshToken);
}
