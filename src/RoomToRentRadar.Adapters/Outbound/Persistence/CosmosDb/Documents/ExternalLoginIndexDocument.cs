using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

/// <summary>
/// Cosmos DB document used as a unique reference index ensuring global uniqueness of external logins
/// (provider + providerSubject) across user partitions. Partition key is /id.
/// </summary>
public sealed class ExternalLoginIndexDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonProperty("providerSubject")]
    public string ProviderSubject { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Builds a deterministic, Cosmos DB-safe document ID from (provider, providerSubject).
    /// Encodes the subject with Base64Url to avoid illegal Cosmos DB ID characters ('/', '\', '?', '#').
    /// Falls back to SHA-256 hex if the encoded ID would exceed 250 characters.
    /// </summary>
    public static string BuildId(string provider, string providerSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubject);

        var normalizedProvider = provider.Trim().ToUpperInvariant();
        var normalizedSubject = providerSubject.Trim();
        var subjectBytes = Encoding.UTF8.GetBytes(normalizedSubject);
        var base64Url = Convert.ToBase64String(subjectBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var candidate = $"{normalizedProvider}_{base64Url}";
        if (candidate.Length > 250)
        {
            var hash = SHA256.HashData(subjectBytes);
            return $"{normalizedProvider}_{Convert.ToHexString(hash)}";
        }

        return candidate;
    }
}
