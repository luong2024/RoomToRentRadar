namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

using Newtonsoft.Json;

public sealed class CampusDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("latitude")]
    public double Latitude { get; set; }

    [JsonProperty("longitude")]
    public double Longitude { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;
}
