using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosUserProfileRepository : IUserProfileRepository
{
    // Độc lập container, dùng chung ICosmosContainer cơ chế có sẵn
    private readonly InMemoryCosmosContainer<UserProfileDocument> _inMemoryContainer = new();
    private readonly Dictionary<Guid, UserProfile> _cache = [];

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }

    public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _cache[profile.UserId] = profile;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _cache[profile.UserId] = profile;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var pending = _cache.Values
            .Where(p => p.Role == UserRole.Landlord && p.Landlord?.VerificationStatus == KycVerificationStatus.Pending)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<UserProfile>>(pending);
    }
}