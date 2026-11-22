namespace RoomToRentRadar.Adapters.Outbound.Storage;

public sealed class PhotoStorageOptions
{
    public const string SectionName = "PhotoStorage";

    public string Provider { get; set; } = "CloudflareR2";

    public string AccountId { get; set; } = string.Empty;

    public string BucketName { get; set; } = "roomtorentradar-photos";

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsCloudflareR2Configured() =>
        string.Equals(Provider, "CloudflareR2", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(BucketName) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
