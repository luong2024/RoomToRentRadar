namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>
/// Strips EXIF (including GPS) from a JPEG/PNG upload without resizing it (zero-NuGet decision,
/// BR-MED-04 / ADR-008). Returns a cleaned stream positioned at 0; the caller disposes it.
/// </summary>
public interface IImageProcessorPort
{
    Task<Stream> ProcessAndStripExifAsync(Stream input, string extension, CancellationToken ct = default);
}
