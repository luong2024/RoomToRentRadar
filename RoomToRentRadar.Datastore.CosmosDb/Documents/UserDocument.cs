using Newtonsoft.Json;

namespace RoomToRentRadar.Datastore.CosmosDb.Documents;

public sealed class UserDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("normalizedEmail")]
    public string NormalizedEmail { get; set; } = string.Empty;

    [JsonProperty("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonProperty("passwordHash")]
    public string? PasswordHash { get; set; }

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("authProvider")]
    public string AuthProvider { get; set; } = string.Empty;

    [JsonProperty("ssoSubjectId")]
    public string? SsoSubjectId { get; set; }

    [JsonProperty("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonProperty("studentCode")]
    public string? StudentCode { get; set; }

    [JsonProperty("isVerified")]
    public bool IsVerified { get; set; }

    [JsonProperty("failedLoginCount")]
    public int FailedLoginCount { get; set; }

    [JsonProperty("lockoutEndUtc")]
    public DateTime? LockoutEndUtc { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonProperty("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    [JsonProperty("refreshTokens")]
    public List<RefreshTokenDocument> RefreshTokens { get; set; } = new();
}

