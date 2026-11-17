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

public sealed class RoomDocument
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

    [JsonProperty("landlordPhone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [JsonProperty("lastUpdatedDate")]
    public DateTime? LastUpdatedDate { get; set; }

    [JsonProperty("latitude")]
    public double? Latitude { get; set; }

    [JsonProperty("longitude")]
    public double? Longitude { get; set; }

    [JsonProperty("location")]
    public CosmosPoint? Location { get; set; }

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
            LandlordPhone = room.LandlordPhone,
            LastUpdatedDate = room.LastUpdatedDate,
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
            LandlordPhone = LandlordPhone,
            LastUpdatedDate = LastUpdatedDate,
            Latitude = lat,
            Longitude = lon
        };
    }
}
