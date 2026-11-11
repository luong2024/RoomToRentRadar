namespace RoomToRentRadar.Domain.Services;

public static class PhotoFileRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxPhotosPerRoom = 10;
    public const int MinPhotosToPublish = 3;

    // SKELETON ở task này — ném NotImplementedException để Task 2 có test RED.
    public static string? DetectExtension(ReadOnlySpan<byte> header) => throw new NotImplementedException();

    // SKELETON ở task này — ném NotImplementedException.
    public static bool IsValidRoomId(string? roomId) => throw new NotImplementedException();
}
