using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Landlord listing management (Landlord tag of <c>docs/api/openapi.yaml</c>, US08, US11, BR-LST-04..05).</summary>
public interface IMyListingGateway
{
    [ApiOperation("uploadPhoto")]
    Task<PhotoUploadDto> UploadPhotoAsync(Stream content, string fileName, string contentType, PhotoKind kind, CancellationToken ct = default);

    [ApiOperation("deletePhoto")]
    Task DeletePhotoAsync(string photoId, CancellationToken ct = default);

    [ApiOperation("listMyListings")]
    Task<PagedMyListings> ListMyListingsAsync(ListingStatus? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("getMyListing")]
    Task<MyListingDetailDto> GetMyListingAsync(string roomId, CancellationToken ct = default);

    [ApiOperation("createListing")]
    Task<MyListingDetailDto> CreateListingAsync(UpsertListingRequest request, CancellationToken ct = default);

    [ApiOperation("updateListing")]
    Task<MyListingDetailDto> UpdateListingAsync(string roomId, UpsertListingRequest request, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("markListingRented")]
    Task<MyListingDetailDto> MarkListingRentedAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("markListingAvailable")]
    Task<MyListingDetailDto> MarkListingAvailableAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("unpublishListing")]
    Task<MyListingDetailDto> UnpublishListingAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("republishListing")]
    Task<MyListingDetailDto> RepublishListingAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("resubmitListing")]
    Task<MyListingDetailDto> ResubmitListingAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    // openapi updateHouseRules has no If-Match parameter, so no expectedVersion here (PLAN §5.3).
    [ApiOperation("updateHouseRules")]
    Task<HouseRulesDto> UpdateHouseRulesAsync(string roomId, UpdateHouseRulesRequest request, CancellationToken ct = default);

    [ApiOperation("listHouseRulesVersions")]
    Task<IReadOnlyList<HouseRulesDto>> ListHouseRulesVersionsAsync(string roomId, CancellationToken ct = default);
}