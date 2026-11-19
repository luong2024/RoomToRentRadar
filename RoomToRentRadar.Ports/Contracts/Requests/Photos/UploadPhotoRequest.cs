using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Ports.Contracts.Requests.Photos;

/// <summary>
/// One multipart photo upload (<c>operationId: uploadPhoto</c>, US08, BR-MED-02..03).
/// <see cref="FileName"/> is informational only: the real format is detected from magic bytes.
/// </summary>
public sealed record UploadPhotoRequest(
    string UserId,
    PhotoKind Kind,
    string FileName,
    Stream Content,
    long Length);
