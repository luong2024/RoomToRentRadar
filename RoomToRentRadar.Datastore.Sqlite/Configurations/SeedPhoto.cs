using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Datastore.Sqlite.Configurations;

/// <summary>
/// One Development seed photo (T00.10). The files already exist under their storage keys; the datastore
/// only writes the <c>RoomPhotos</c> rows (erd §4.8).
/// </summary>
/// <param name="RoomType">The room type this photo illustrates, or <c>null</c> for a supplementary shot
/// (bathroom) that any room can use.</param>
public sealed record SeedPhoto(
    RoomType? RoomType,
    string ThumbPath,
    string MediumPath,
    int Width,
    int Height,
    long SizeBytes);
