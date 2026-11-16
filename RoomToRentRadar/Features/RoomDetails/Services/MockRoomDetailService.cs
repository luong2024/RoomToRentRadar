using RoomToRentRadar.Features.RoomDetails.Models;

namespace RoomToRentRadar.Features.RoomDetails.Services;

public class MockRoomDetailService : IRoomDetailService
{
    public Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var status = roomId switch
        {
            "rented" => ListingStatus.Rented,
            "unavailable" => ListingStatus.Unavailable,
            _ => ListingStatus.Available
        };

        var room = new RoomDetailModel
        {
            Id = roomId,
            Title = "2 Bedroom Apartment",
            Description = "A beautiful modern apartment located at Kunle Ajasin estate, Akobo, Ibadan. Close to campus, 24/7 security surveillance, perimeter fencing, treated water supply, and uninterrupted power supply.",
            Price = 450000,
            Deposit = 15000,
            AreaSquareMeters = 35,
            RoomType = "2 Bedroom Modern Flat",
            Address = "Kunle Ajasin estate, Akobo, Ibadan",
            Status = status,
            LastUpdated = DateTime.Now.AddDays(-1),
            SittingRooms = 1,
            Bedrooms = 2,
            Bathrooms = 2,
            Kitchens = 1,
            Rating = 4.95,
            ReviewCount = 22,
            LandlordName = "John Adebayo",
            LandlordPhone = "+234 0000 0000 000",
            LandlordEmail = "johnade45@gmail.com",
            LandlordAvatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=200",
            ImageUrls =
            [
                "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=1000",
                "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000",
                "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=1000"
            ],
            Amenities =
            [
                new() { Name = "Pool side", IconGlyph = "🏊" },
                new() { Name = "Parking space", IconGlyph = "🚗" },
                new() { Name = "Free Wi-Fi", IconGlyph = "📶" },
                new() { Name = "Air conditioning", IconGlyph = "❄️" },
                new() { Name = "Garden", IconGlyph = "🌿" },
                new() { Name = "Security", IconGlyph = "🛡️" }
            ],
            HouseRules =
            [
                "Quiet hours after 11:00 PM",
                "Maintain cleanliness in common spaces",
                "No loud parties without prior notice"
            ]
        };

        return Task.FromResult<RoomDetailModel?>(room);
    }

    public Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var list = new List<SimilarRoomModel>
        {
            new()
            {
                Id = "sim-1",
                Title = "1 Bedroom Modern Flat",
                Price = 380000,
                AreaSquareMeters = 25,
                Address = "Akobo Estate, Ibadan",
                ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=400"
            },
            new()
            {
                Id = "sim-2",
                Title = "Studio Apartment Near Campus",
                Price = 280000,
                AreaSquareMeters = 20,
                Address = "University Road, Ibadan",
                ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=400"
            }
        };

        return Task.FromResult(list);
    }
}