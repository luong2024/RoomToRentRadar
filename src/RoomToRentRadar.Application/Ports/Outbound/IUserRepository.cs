using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound persistence port for <see cref="User"/> aggregates.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    Task<User?> GetByExternalLoginAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);
}
