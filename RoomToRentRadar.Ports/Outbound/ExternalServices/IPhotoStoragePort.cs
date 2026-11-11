namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IPhotoStoragePort
{
    Task<string> SaveAsync(string roomId, string fileName, Stream content, CancellationToken ct = default); // trả URL tương đối
    Task DeleteAsync(string roomId, string fileName, CancellationToken ct = default);
}
