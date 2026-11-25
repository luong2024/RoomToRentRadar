namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IListingImageStorageService
{
    Task<string> StoreAsync(
        Guid ownerId,
        string listingId,
        Stream content,
        string fileName,
        string contentType,
        long length,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default);
}
