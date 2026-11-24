namespace RoomToRentRadar.Application.Ports.Inbound;

public sealed record ListingImageUpload(Func<Stream> OpenReadStream, string FileName, string ContentType, long Length);

public sealed record ListingWriteRequest(
    string? Title,
    string? Address,
    decimal MonthlyRentVnd,
    decimal DepositVnd,
    double AreaM2,
    string? RoomType,
    string? Description,
    IReadOnlyList<ListingImageUpload> Photos,
    IReadOnlyList<string>? ImageUrlsToRetain = null);

public sealed record ListingWriteResult(string ListingId, string Status, IReadOnlyList<string> ImageUrls);

public interface ICreateListingUseCase
{
    Task<ListingWriteResult> ExecuteAsync(Guid ownerId, ListingWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateListingUseCase
{
    Task<ListingWriteResult> ExecuteAsync(Guid ownerId, string listingId, ListingWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IUnpublishListingUseCase
{
    Task ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default);
}
