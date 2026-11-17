using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Listing-management stubs (PLAN §5.4): photos T03.1, create/update T08.2,
/// lifecycle T08.3, house rules T11.1.
/// </summary>
public sealed class HttpMyListingGateway : IMyListingGateway
{
    public HttpMyListingGateway(HttpClient httpClient, ILogger<HttpMyListingGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<PhotoUploadDto> UploadPhotoAsync(Stream content, string fileName, string contentType, PhotoKind kind, CancellationToken ct = default) =>
        Pending.For<PhotoUploadDto>("T03.1", "uploadPhoto");

    public Task DeletePhotoAsync(string photoId, CancellationToken ct = default) =>
        Pending.For("T03.1", "deletePhoto");

    public Task<PagedMyListings> ListMyListingsAsync(ListingStatus? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedMyListings>("T08.3", "listMyListings");

    public Task<MyListingDetailDto> GetMyListingAsync(string roomId, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "getMyListing");

    public Task<MyListingDetailDto> CreateListingAsync(UpsertListingRequest request, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.2", "createListing");

    public Task<MyListingDetailDto> UpdateListingAsync(string roomId, UpsertListingRequest request, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.2", "updateListing");

    public Task<MyListingDetailDto> MarkListingRentedAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "markListingRented");

    public Task<MyListingDetailDto> MarkListingAvailableAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "markListingAvailable");

    public Task<MyListingDetailDto> UnpublishListingAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "unpublishListing");

    public Task<MyListingDetailDto> RepublishListingAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "republishListing");

    public Task<MyListingDetailDto> ResubmitListingAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.3", "resubmitListing");

    public Task<HouseRulesDto> UpdateHouseRulesAsync(string roomId, UpdateHouseRulesRequest request, CancellationToken ct = default) =>
        Pending.For<HouseRulesDto>("T11.1", "updateHouseRules");

    public Task<IReadOnlyList<HouseRulesDto>> ListHouseRulesVersionsAsync(string roomId, CancellationToken ct = default) =>
        Pending.For<IReadOnlyList<HouseRulesDto>>("T11.1", "listHouseRulesVersions");
}