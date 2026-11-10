using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public EfUserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByExternalLoginAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.Trim().ToUpperInvariant();
        var normalizedSubject = providerSubject.Trim();

        return await _dbContext.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(
                u => u.ExternalLogins.Any(
                    el => el.Provider == normalizedProvider && el.ProviderSubject == normalizedSubject),
                cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public void Update(User user)
    {
        _dbContext.Users.Update(user);
    }
}
