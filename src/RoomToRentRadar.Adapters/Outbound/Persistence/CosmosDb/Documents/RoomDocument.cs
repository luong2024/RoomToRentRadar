using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class CosmosPoint
{
    [JsonProperty("type")]
    public string Type { get; set; } = "Point";

    [JsonProperty("coordinates")]
    public double[] Coordinates { get; set; } = [0, 0]; // [longitude, latitude]
}

public sealed class RoomDocument : IProjectionVersionedDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("area")]
    public double Area { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonProperty("imageUrls")]
    public List<string> ImageUrls { get; set; } = [];

    [JsonProperty("isVerified")]
    public bool IsVerified { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("deposit")]
    public decimal? Deposit { get; set; }

    [JsonProperty("roomType")]
    public string RoomType { get; set; } = string.Empty;

    [JsonProperty("bedrooms")]
    public int? Bedrooms { get; set; }

    [JsonProperty("bathrooms")]
    public int? Bathrooms { get; set; }

    [JsonProperty("amenities")]
    public List<string> Amenities { get; set; } = [];

    [JsonProperty("houseRules")]
    public List<string> HouseRules { get; set; } = [];

    [JsonProperty("landlordId")]
    public Guid? LandlordId { get; set; }

    [JsonProperty("landlordPhone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("lastUpdatedDate")]
    public DateTime? LastUpdatedDate { get; set; }

    [JsonProperty("latitude")]
    public double? Latitude { get; set; }

    [JsonProperty("longitude")]
    public double? Longitude { get; set; }

    [JsonProperty("location")]
    public CosmosPoint? Location { get; set; }

    [JsonProperty("projectionVersion")]
    public long ProjectionVersion { get; set; }

    [JsonProperty("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    /// <summary>
    /// Applies only fields owned by <see cref="RoomListingDocument"/> to an
    /// existing detail document. Detail-only fields, including the gallery,
    /// deposit, room type, amenities, house rules, contact details, and room
    /// counts, must survive listing synchronization.
    /// </summary>
    public static RoomDocument MergeListingFields(
        RoomListingDocument listing,
        RoomDocument? existingDetail)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var detail = existingDetail ?? new RoomDocument { Id = listing.Id };

        detail.Id = listing.Id;
        detail.Title = listing.Title;
        detail.Description = listing.Description;
        detail.Price = listing.MonthlyRentAmount;
        detail.Area = listing.AreaSquareMeters;
        detail.Address = listing.Address;
        detail.ThumbnailUrl = listing.ThumbnailUrl;
        detail.IsVerified = listing.VerificationStatus == 2;
        detail.Status = listing.Status;
        detail.LandlordId ??= listing.LandlordId;
        // A listing's creation timestamp is only a valid initial value. It must
        // never overwrite the detail record's actual last-update timestamp.
        detail.LastUpdatedDate ??= listing.CreatedAtUtc;
        detail.Latitude = listing.Latitude;
        detail.Longitude = listing.Longitude;
        detail.Location = new CosmosPoint
        {
            Coordinates = [listing.Longitude, listing.Latitude]
        };

        // A new document has no gallery yet, so its thumbnail is a reasonable
        // initial image. Existing galleries are never replaced by listing data.
        if (existingDetail is null && !string.IsNullOrWhiteSpace(listing.ThumbnailUrl))
        {
            detail.ImageUrls = [listing.ThumbnailUrl];
        }

        return detail;
    }

    public static RoomDocument FromDomain(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        CosmosPoint? location = null;
        if (room.Latitude.HasValue && room.Longitude.HasValue)
        {
            location = new CosmosPoint
            {
                Type = "Point",
                Coordinates = [room.Longitude.Value, room.Latitude.Value]
            };
        }

        return new RoomDocument
        {
            Id = room.Id,
            Title = room.Title,
            Description = room.Description,
            Price = room.Price,
            Area = room.Area,
            Address = room.Address,
            ThumbnailUrl = room.ThumbnailUrl,
            ImageUrls = room.ImageUrls.ToList(),
            IsVerified = room.IsVerified,
            Status = (int)room.Status,
            Deposit = room.Deposit,
            RoomType = room.RoomType,
            Bedrooms = room.Bedrooms,
            Bathrooms = room.Bathrooms,
            Amenities = room.Amenities.ToList(),
            HouseRules = room.HouseRules.ToList(),
            LandlordId = room.LandlordId,
            LandlordPhone = room.LandlordPhone,
            LastUpdatedDate = room.LastUpdatedDate,
            CreatedAt = room.CreatedAt != default ? room.CreatedAt : (room.LastUpdatedDate ?? DateTime.UtcNow),
            Latitude = room.Latitude,
            Longitude = room.Longitude,
            Location = location
        };
    }

    public Room ToDomain()
    {
        double? lat = Latitude;
        double? lon = Longitude;

        if ((!lat.HasValue || !lon.HasValue) && Location?.Coordinates is { Length: >= 2 } coords)
        {
            lon = coords[0];
            lat = coords[1];
        }

        return new Room
        {
            Id = Id,
            Title = Title,
            Description = Description,
            Price = Price,
            Area = Area,
            Address = Address,
            ThumbnailUrl = ThumbnailUrl,
            ImageUrls = ImageUrls ?? [],
            IsVerified = IsVerified,
            Status = (RoomStatus)Status,
            Deposit = Deposit,
            RoomType = RoomType,
            Bedrooms = Bedrooms,
            Bathrooms = Bathrooms,
            Amenities = Amenities ?? [],
            HouseRules = HouseRules ?? [],
            LandlordId = LandlordId,
            LandlordPhone = LandlordPhone,
            LastUpdatedDate = LastUpdatedDate,
            CreatedAt = CreatedAt != default ? CreatedAt : (LastUpdatedDate ?? DateTime.UtcNow),
            Latitude = lat,
            Longitude = lon
        };
    }
}
