namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;

public sealed class CosmosDbSettings
{
    public const string SectionName = "CosmosDb";

    public string AccountEndpoint { get; set; } = "https://localhost:8081/";
    public string AccountKey { get; set; } = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    public string DatabaseName { get; set; } = "RoomToRentRadarDb";
    public string RoomsContainerName { get; set; } = "Rooms";
    public string CampusesContainerName { get; set; } = "Campuses";
    public bool IsEmulator { get; set; } = true;
}
