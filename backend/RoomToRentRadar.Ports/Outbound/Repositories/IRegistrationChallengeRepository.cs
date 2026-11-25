using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IRegistrationChallengeRepository
{
    Task AddAsync(RegistrationChallenge challenge, CancellationToken cancellationToken);

    Task<RegistrationChallenge?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    void Remove(RegistrationChallenge challenge);
}
