using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.RoomDetails.Services;

/// <summary>
/// Fetches room details and similar rooms from the backend API.
/// </summary>
public sealed class RoomDetailService : IRoomDetailService
{
    private readonly IApiClient _apiClient;

    public RoomDetailService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var endpoint = $"api/rooms/{Uri.EscapeDataString(roomId.Trim())}";
        var dto = await _apiClient.GetAsync<RoomDetailApiDto>(endpoint, cancellationToken);
        return dto is null ? null : MapDtoToModel(dto);
    }

    public async Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var detail = await GetRoomDetailAsync(roomId, cancellationToken);
        return detail?.SimilarRooms ?? [];
    }

    private static RoomDetailModel MapDtoToModel(RoomDetailApiDto dto)
    {
        var status = ParseStatus(dto.Status, dto.IsAvailable);

        return new RoomDetailModel
        {
        Id = dto.Id,
        Title = dto.Title,
        Description = dto.Description,
        Price = dto.Price,
        Deposit = dto.Deposit,
        AreaSquareMeters = dto.Area,
        RoomType = dto.RoomType,
        Bedrooms = dto.Bedrooms,
        Bathrooms = dto.Bathrooms,
        Address = dto.Address,
        Status = status,
        LastUpdated = dto.LastUpdatedDate,
        IsVerified = dto.IsVerified,
        IsAvailable = status == ListingStatus.Available,
        UnavailableMessage = dto.UnavailableMessage,
        ImageUrls = dto.ImageUrls ?? [],
        LandlordPhone = dto.LandlordPhone,
        HouseRules = dto.HouseRules ?? [],
        Amenities = (dto.Amenities ?? []).Select(amenity => new RoomAmenityModel
        {
            Name = amenity,
            IconGlyph = ResolveAmenityGlyph(amenity)
        }).ToList(),
        SimilarRooms = (dto.SimilarRooms ?? []).Select(similar => new SimilarRoomModel
        {
            Id = similar.Id,
            Title = similar.Title,
            Price = similar.Price,
            AreaSquareMeters = similar.Area,
            Address = similar.Address,
            ThumbnailUrl = similar.ThumbnailUrl,
            RoomType = similar.RoomType,
            IsVerified = similar.IsVerified
        }).ToList()
        };
    }

    private static ListingStatus ParseStatus(string? status, bool? isAvailable)
    {
        if (string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase))
        {
            return ListingStatus.Available;
        }

        if (string.Equals(status, "Rented", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Occupied", StringComparison.OrdinalIgnoreCase))
        {
            return ListingStatus.Rented;
        }

        if (string.Equals(status, "Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return ListingStatus.Unavailable;
        }

        return isAvailable is true
            ? ListingStatus.Available
            : ListingStatus.Unavailable;
    }

    private static string ResolveAmenityGlyph(string amenity)
    {
        var lower = amenity.ToLowerInvariant();
        if (lower.Contains("wifi")) return "\uf8fe";
        if (lower.Contains("air condition") || lower.Contains("điều hòa")) return "\uf15f";
        if (lower.Contains("water heater") || lower.Contains("nóng lạnh")) return "\uf3e2";
        if (lower.Contains("washer") || lower.Contains("giặt")) return "\uf893";
        if (lower.Contains("parking") || lower.Contains("bãi xe")) return "\uf8c4";
        if (lower.Contains("security") || lower.Contains("camera") || lower.Contains("an ninh")) return "\uf6a4";
        return "\uf2c8";
    }
}
