using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IRegistrationChallengeRepository
{
    Task AddAsync(RegistrationChallenge challenge, CancellationToken cancellationToken);

    Task<RegistrationChallenge?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically increments the attempt counter while it is below <paramref name="maxAttempts"/>
    /// and returns the stored count, or <c>null</c> when the cap is already reached or the
    /// challenge no longer exists.
    /// </summary>
    Task<int?> TryConsumeAttemptAsync(
        Guid challengeId,
        int maxAttempts,
        CancellationToken cancellationToken);

    Task RemoveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task RemoveExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    void Remove(RegistrationChallenge challenge);
}
