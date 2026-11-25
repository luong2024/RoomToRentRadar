using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class RegistrationChallengeRepository(AppDbContext dbContext)
    : IRegistrationChallengeRepository
{
    public async Task AddAsync(RegistrationChallenge challenge, CancellationToken cancellationToken)
        => await dbContext.RegistrationChallenges.AddAsync(challenge, cancellationToken);

    public Task<RegistrationChallenge?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.RegistrationChallenges.FirstOrDefaultAsync(
            challenge => challenge.Id == id,
            cancellationToken);

    public async Task<int?> TryConsumeAttemptAsync(
        Guid challengeId,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.RegistrationChallenges
            .Where(challenge => challenge.Id == challengeId && challenge.AttemptCount < maxAttempts)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    challenge => challenge.AttemptCount,
                    challenge => challenge.AttemptCount + 1),
                cancellationToken);
        if (affected == 0)
        {
            return null;
        }

        return await dbContext.RegistrationChallenges
            .AsNoTracking()
            .Where(challenge => challenge.Id == challengeId)
            .Select(challenge => (int?)challenge.AttemptCount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task RemoveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => dbContext.RegistrationChallenges
            .Where(challenge => challenge.Email == normalizedEmail)
            .ExecuteDeleteAsync(cancellationToken);

    public Task RemoveExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        => dbContext.RegistrationChallenges
            .Where(challenge => challenge.ExpiresAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public void Remove(RegistrationChallenge challenge)
        => dbContext.RegistrationChallenges.Remove(challenge);
}
