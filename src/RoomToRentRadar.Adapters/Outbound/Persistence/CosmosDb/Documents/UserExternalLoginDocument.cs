using Newtonsoft.Json;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class UserExternalLoginDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonProperty("providerSubject")]
    public string ProviderSubject { get; set; } = string.Empty;

    [JsonProperty("providerEmail")]
    public string ProviderEmail { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
