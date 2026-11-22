namespace RoomToRentRadar.Ports.Models.Photos;

/// <summary>An upload stream owned by the caller; the use case only reads it.</summary>
public sealed record PhotoUpload(string FileName, string ContentType, long Length, Stream Content);

public sealed record ProcessedPhoto(byte[] Original, byte[] Thumbnail, int Width, int Height);
