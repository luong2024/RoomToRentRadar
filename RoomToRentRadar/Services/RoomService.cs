using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

public class RoomService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://10.0.2.2:5000/api";

    public RoomService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<RoomDetailDto> GetRoomDetailAsync(int roomId)
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync("auth_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            var result = await _httpClient.GetFromJsonAsync<RoomDetailDto>($"{BaseUrl}/rooms/{roomId}");
            if (result != null) return result;
        }
        catch
        {
            // Fallback to mock data while API endpoint is not ready
        }

        return GetMockRoomDetail(roomId);
    }

    private static RoomDetailDto GetMockRoomDetail(int roomId) => new()
    {
        Id = roomId,
        Title = "Self-contained student room near ICTU back gate",
        Description = "Airy room with private balcony, fingerprint lock with 24/7 security, 300m from ICTU campus.",
        MonthlyRent = 1500000,
        Deposit = 1500000,
        Area = 22.5,
        RoomType = "Studio / Self-contained",
        Address = "Lane 12, Z115 Street, Quyet Thang Ward, Thai Nguyen",
        Status = "Available",
        IsVerified = true,
        LastUpdated = DateTime.Now.AddDays(-1),
        LandlordName = "Nguyen Van Hung",
        LandlordPhone = "0987654321",
        Amenities = new List<string> { "High-speed Wi-Fi", "Air Conditioner", "Water Heater", "Free Parking", "Security Camera" },
        HouseRules = new List<string>
        {
            "Gate closes at 23:30 daily (fingerprint access available)",
            "Cooking allowed inside room using induction/electric stove",
            "No loud noise after 22:00",
            "Overnight guests must be reported to the landlord in advance"
        },
        SimilarRooms = new List<RoomSummaryDto>
        {
            new() { Id = 2, Title = "Student room in Lane 5 Tan Thinh", MonthlyRent = 1300000, Area = 20, Address = "Tan Thinh Ward" },
            new() { Id = 3, Title = "Furnished mini apartment in Thinh Dan", MonthlyRent = 1800000, Area = 25, Address = "Thinh Dan Ward" }
        }
    };
}
