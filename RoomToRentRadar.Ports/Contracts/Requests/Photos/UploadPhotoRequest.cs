using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Requests.Photos;

public sealed record UploadPhotoRequest(
    string UserId,
    PhotoKind Kind,
    string FileName,
    Stream Content,
    long Length);
