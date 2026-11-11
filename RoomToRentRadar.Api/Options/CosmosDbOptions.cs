namespace RoomToRentRadar.Api.Options;

public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "RoomToRentRadar";

    public string ListingsContainer { get; set; } = "Listings";
}
