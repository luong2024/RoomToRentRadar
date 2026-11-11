namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IPhotoStoragePort
{
    Task SaveAsync(string path, Stream content, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    string GetPublicUrl(string path);
}
