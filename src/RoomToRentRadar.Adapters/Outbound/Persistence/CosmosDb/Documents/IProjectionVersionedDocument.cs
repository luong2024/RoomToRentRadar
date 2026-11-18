namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public interface IProjectionVersionedDocument
{
    long ProjectionVersion { get; set; }

    bool IsDeleted { get; set; }

    string? ETag { get; set; }
}
