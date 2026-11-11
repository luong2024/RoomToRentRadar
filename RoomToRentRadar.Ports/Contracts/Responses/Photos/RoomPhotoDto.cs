namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

public sealed record RoomPhotoDto(
    string Id,
    string Url,
    bool IsCover,
    DateTime UploadedAtUtc);
