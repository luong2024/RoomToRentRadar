namespace RoomToRentRadar.Infrastructure.Adapters.Media;

/// <summary>
/// Local-disk media store configuration (ADR-008). <see cref="RootPath"/> is the directory served by
/// the API at <c>/media/...</c>; <see cref="PublicBaseUrl"/> is the absolute origin used in DTO URLs.
/// </summary>
public sealed class MediaSettings
{
    public const string SectionName = "Media";

    public string RootPath { get; set; } = "media";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
}
