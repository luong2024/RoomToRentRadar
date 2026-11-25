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

    public void Remove(RegistrationChallenge challenge)
        => dbContext.RegistrationChallenges.Remove(challenge);
}
