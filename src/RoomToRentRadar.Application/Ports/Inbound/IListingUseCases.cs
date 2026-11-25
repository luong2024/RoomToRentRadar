namespace RoomToRentRadar.Application.Ports.Inbound;

using RoomToRentRadar.Domain.ValueObjects;

public static class ListingPhotoLimits
{
    public const int MaxCount = 20;
    public const long MaxBytesPerPhoto = 10L * 1024 * 1024;
}

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
    IReadOnlyList<string>? ImageUrlsToRetain = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ListingWriteResult(string ListingId, string Status, IReadOnlyList<string> ImageUrls, GeoLocation? Location = null);

public sealed record ListingCoordinatesResult(string ListingId, GeoLocation? Location);

public interface IGetListingCoordinatesUseCase
{
    Task<ListingCoordinatesResult> ExecuteAsync(Guid ownerId, string listingId, CancellationToken cancellationToken = default);
}

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
