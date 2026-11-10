using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Datastore.CosmosDb.Context;
using RoomToRentRadar.Datastore.CosmosDb.Documents;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;
using User = RoomToRentRadar.Domain.Aggregates.User;

namespace RoomToRentRadar.Infrastructure.Repositories;

public sealed class CosmosUserRepository : IUserRepository
{
    private readonly CosmosDbContext _dbContext;

    public CosmosUserRepository(CosmosDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var container = _dbContext.GetUsersContainer();
        try
        {
            var response = await container.ReadItemAsync<UserDocument>(
                id,
                new PartitionKey(id),
                cancellationToken: ct);
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var normalized = normalizedEmail.Trim().ToLowerInvariant();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.normalizedEmail = @email")
            .WithParameter("@email", normalized);

        var doc = await QuerySingleAsync(query, ct);
        return doc == null ? null : MapToDomain(doc);
    }

    public async Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE ARRAY_CONTAINS(c.refreshTokens, { \"tokenHash\": @tokenHash }, true)")
            .WithParameter("@tokenHash", refreshTokenHash);

        var doc = await QuerySingleAsync(query, ct);
        return doc == null ? null : MapToDomain(doc);
    }

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var existing = await GetByEmailAsync(normalizedEmail, ct);
        return existing != null;
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var container = _dbContext.GetUsersContainer();
        var document = MapToDocument(user);
        try
        {
            await container.CreateItemAsync(
                document,
                new PartitionKey(document.Id),
                cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new DuplicateEmailException(user.Email.Value);
        }
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var container = _dbContext.GetUsersContainer();
        var document = MapToDocument(user);
        await container.UpsertItemAsync(
            document,
            new PartitionKey(document.Id),
            cancellationToken: ct);
    }

    private async Task<UserDocument?> QuerySingleAsync(QueryDefinition query, CancellationToken ct)
    {
        var container = _dbContext.GetUsersContainer();
        using var iterator = container.GetItemQueryIterator<UserDocument>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            var first = page.FirstOrDefault();
            if (first != null)
            {
                return first;
            }
        }

        return null;
    }

    public static UserDocument MapToDocument(User user)
    {
        return new UserDocument
        {
            Id = user.Id,
            Email = user.Email.Value,
            NormalizedEmail = user.Email.Value,
            FullName = user.FullName,
            PasswordHash = user.PasswordHash,
            Role = user.Role.ToString(),
            AuthProvider = user.AuthProvider.ToString(),
            SsoSubjectId = user.SsoSubjectId,
            PhoneNumber = user.PhoneNumber,
            StudentCode = user.StudentCode,
            IsVerified = user.IsVerified,
            FailedLoginCount = user.FailedLoginCount,
            LockoutEndUtc = user.LockoutEndUtc,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc,
            RefreshTokens = user.RefreshTokens.Select(t => new RefreshTokenDocument
            {
                TokenHash = t.TokenHash,
                JwtId = t.JwtId,
                CreatedAtUtc = t.CreatedAtUtc,
                ExpiresAtUtc = t.ExpiresAtUtc,
                RevokedAtUtc = t.RevokedAtUtc,
                ReplacedByTokenHash = t.ReplacedByTokenHash
            }).ToList()
        };
    }

    public static User MapToDomain(UserDocument doc)
    {
        var role = Enum.Parse<UserRole>(doc.Role, ignoreCase: true);
        var provider = Enum.Parse<AuthProvider>(doc.AuthProvider, ignoreCase: true);
        var tokens = doc.RefreshTokens.Select(t => new RefreshTokenSession(
            tokenHash: t.TokenHash,
            jwtId: t.JwtId,
            createdAtUtc: t.CreatedAtUtc,
            expiresAtUtc: t.ExpiresAtUtc,
            revokedAtUtc: t.RevokedAtUtc,
            replacedByTokenHash: t.ReplacedByTokenHash));

        return User.Reconstitute(
            id: doc.Id,
            email: doc.Email,
            fullName: doc.FullName,
            passwordHash: doc.PasswordHash,
            role: role,
            authProvider: provider,
            ssoSubjectId: doc.SsoSubjectId,
            phoneNumber: doc.PhoneNumber,
            studentCode: doc.StudentCode,
            isVerified: doc.IsVerified,
            failedLoginCount: doc.FailedLoginCount,
            lockoutEndUtc: doc.LockoutEndUtc,
            createdAtUtc: doc.CreatedAtUtc,
            updatedAtUtc: doc.UpdatedAtUtc,
            refreshTokens: tokens);
    }
}
