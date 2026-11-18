using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class FavouriteDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonProperty("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [JsonProperty("_etag", NullValueHandling = NullValueHandling.Ignore)]
    public string? ETag { get; set; }

    public static FavouriteDocument FromDomain(FavouriteRoom favourite)
    {
        ArgumentNullException.ThrowIfNull(favourite);

        return new FavouriteDocument
        {
            Id = favourite.Id,
            UserId = favourite.UserId.ToString("D"),
            RoomId = favourite.RoomId,
            CreatedAtUtc = favourite.CreatedAtUtc
        };
    }

    public FavouriteRoom ToDomain()
    {
        if (!Guid.TryParse(UserId, out var parsedUserId) || parsedUserId == Guid.Empty)
        {
            throw new InvalidOperationException($"FavouriteDocument '{Id}' has an invalid UserId '{UserId}'.");
        }

        return FavouriteRoom.Create(parsedUserId, RoomId, CreatedAtUtc);
    }
}
