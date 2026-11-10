namespace RoomToRentRadar.Datastore.CosmosDb.Configurations;

public sealed class CosmosDbSettings
{
    public const string SectionName = "CosmosDb";

    public string EndpointUri { get; set; } = string.Empty;
    public string? PrimaryKey { get; set; }
    public string DatabaseName { get; set; } = "RoomToRentRadarDb";
    public string ContainerName { get; set; } = "Rooms";
    public string UsersContainerName { get; set; } = "Users";
}

