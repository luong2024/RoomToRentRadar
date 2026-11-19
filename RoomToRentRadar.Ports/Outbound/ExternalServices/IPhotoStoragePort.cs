namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

/// <summary>
/// Local-disk media store behind a port (ADR-008). Keys are relative paths such as
/// <c>rooms/2026/11/{photoId}/original.jpg</c>; <see cref="GetPublicUrl"/> turns a key into the
/// absolute URL returned in DTOs.
/// </summary>
public interface IPhotoStoragePort
{
    Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    string GetPublicUrl(string storageKey);
}
