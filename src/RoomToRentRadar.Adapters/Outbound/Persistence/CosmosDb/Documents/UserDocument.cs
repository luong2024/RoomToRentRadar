using Newtonsoft.Json;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class UserDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

    [JsonProperty("passwordHash")]
    public string? PasswordHash { get; set; }

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("isActive")]
    public bool IsActive { get; set; }

    [JsonProperty("securityStamp")]
    public string SecurityStamp { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("lastLoginAt")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [JsonProperty("externalLogins")]
    public List<UserExternalLoginDocument> ExternalLogins { get; set; } = [];

    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
