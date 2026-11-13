using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;

public sealed class CosmosUserProfileRepository : IUserProfileRepository
{
    private readonly ICosmosDbContext _dbContext;

    public CosmosUserProfileRepository(ICosmosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<UserProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var idString = userId.ToString();

        var doc = await _dbContext.UserProfiles.ReadItemAsync(
            idString,
            idString,
            cancellationToken);

        return doc?.ToDomain();
    }

    public async Task AddAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var doc = UserProfileDocument.FromDomain(profile);

        // ETag do Cosmos DB sinh ra, không phải dữ liệu do ứng dụng tự ghi.
        doc.ETag = null;

        try
        {
            var created = await _dbContext.UserProfiles.CreateItemAsync(
                doc,
                doc.Id,
                cancellationToken);

            profile.SetVersion(created.ETag);
        }
        catch (CosmosException ex)
            when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConcurrencyConflictException(
                $"Profile for user '{profile.UserId}' already exists.",
                ex);
        }
    }

    public async Task UpdateAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var expectedEtag = profile.Version;

        // Không được update một profile chưa có ETag.
        // Nếu chưa có ETag, phải tạo bằng AddAsync.
        if (string.IsNullOrWhiteSpace(expectedEtag))
        {
            throw new InvalidOperationException(
                "Cannot update a UserProfile without an ETag. " +
                "Create the profile using AddAsync first.");
        }

        var doc = UserProfileDocument.FromDomain(profile);

        // ETag được gửi qua IfMatchEtag, không ghi đè system property.
        doc.ETag = null;

        try
        {
            var updated = await _dbContext.UserProfiles.ReplaceItemAsync(
                doc,
                doc.Id,
                doc.Id,
                ifMatchEtag: expectedEtag,
                cancellationToken: cancellationToken);

            // Cập nhật version mới cho Domain Entity.
            profile.SetVersion(updated.ETag);
        }
        catch (CosmosException ex)
            when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException(
                $"Profile for user '{profile.UserId}' " +
                "was modified concurrently by another operation.",
                ex);
        }
    }

    public async Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var safeSkip = Math.Max(0, skip);
        var safeTake = Math.Clamp(take, 1, 100);

        var pendingStatus = KycVerificationStatus.Pending.ToString();
        var landlordRole = UserRoleExtensions.LandlordRoleName;

        if (_dbContext.UserProfiles is InMemoryCosmosContainer<UserProfileDocument> inMemory)
        {
            var match = await inMemory.QueryAsync(
                p => string.Equals(p.Role, landlordRole, StringComparison.OrdinalIgnoreCase) &&
                     p.Landlord != null &&
                     string.Equals(p.Landlord.VerificationStatus, pendingStatus, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            var inMemoryDocs = match
                .OrderByDescending(p => p.UpdatedAt)
                .ThenBy(p => p.Id)
                .Skip(safeSkip)
                .Take(safeTake)
                .ToList();

            return inMemoryDocs.Select(d => d.ToDomain()).ToList();
        }

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.role = @role AND c.landlord.verificationStatus = @status ORDER BY c.updatedAt DESC, c.id ASC OFFSET @skip LIMIT @take")
            .WithParameter("@role", landlordRole)
            .WithParameter("@status", pendingStatus)
            .WithParameter("@skip", safeSkip)
            .WithParameter("@take", safeTake);

        var results = await _dbContext.UserProfiles.QueryAsync(query, cancellationToken);
        return results.Select(d => d.ToDomain()).ToList();
    }
}
