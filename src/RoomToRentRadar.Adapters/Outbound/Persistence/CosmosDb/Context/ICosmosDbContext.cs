using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public interface ICosmosDbContext : IDisposable
{
    ICosmosContainer<UserDocument> Users { get; }

    ICosmosContainer<RefreshTokenDocument> RefreshTokens { get; }

    ICosmosContainer<EmailIndexDocument> EmailIndex { get; }

    Task EnsureDatabaseAndContainersAsync(CancellationToken cancellationToken = default);
}
