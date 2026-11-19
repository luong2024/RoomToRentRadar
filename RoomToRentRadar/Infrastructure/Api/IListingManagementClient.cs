using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Infrastructure.Api;

public interface IListingManagementClient
{
    Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<ManagedListingDto> GetManagedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManagedListingDto> CreateAsync(ListingFormSubmission form, CancellationToken cancellationToken = default);
    Task<ManagedListingDto> UpdateAsync(Guid id, ListingFormSubmission form, CancellationToken cancellationToken = default);
    Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListingFormSubmission(
    string Title,
    string Address,
    long Price,
    long Deposit,
    double AreaM2,
    RoomType RoomType,
    double Lat,
    double Lng,
    IReadOnlyList<ListingPhotoUpload> Photos,
    string? Description = null);

// Reopen the source for every attempt so a failed upload can be retried.
public sealed record ListingPhotoUpload(string FileName, string ContentType, Func<Task<Stream>> OpenReadAsync);
