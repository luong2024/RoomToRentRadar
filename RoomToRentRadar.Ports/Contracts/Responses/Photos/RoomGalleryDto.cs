namespace RoomToRentRadar.Ports.Contracts.Responses.Photos;

public sealed record RoomGalleryDto(
    string RoomId,
    IReadOnlyList<RoomPhotoDto> Photos,
    int TotalCount,
    bool HasMinimumPhotos);
