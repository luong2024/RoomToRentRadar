namespace RoomToRentRadar.Ports.Contracts.Requests.Photos;

public sealed record UploadRoomPhotosRequest(
    string RoomId,
    string UploadedByUserId,
    IReadOnlyList<PhotoFileInput> Files);
