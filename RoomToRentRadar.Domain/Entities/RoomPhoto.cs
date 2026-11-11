namespace RoomToRentRadar.Domain.Entities;

public sealed class RoomPhoto
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string RoomId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;      // {guid}.jpg|png
    public string Url { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string UploadedByUserId { get; init; } = string.Empty;
    public DateTime UploadedAtUtc { get; init; } = DateTime.UtcNow;
}
