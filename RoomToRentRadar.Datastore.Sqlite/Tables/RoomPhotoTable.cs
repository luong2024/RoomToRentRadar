namespace RoomToRentRadar.Datastore.Sqlite.Tables;

public sealed class RoomPhotoTable
{
    public string Id { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
}
