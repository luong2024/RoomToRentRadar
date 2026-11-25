using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Profile;

/// <summary>
/// Session-scoped stand-in for the profile persistence the API does not expose yet.
/// Data survives navigation but not an app restart.
/// </summary>
public sealed class InMemoryProfileDetailsStore : IProfileDetailsStore
{
    private readonly Dictionary<Guid, StudentProfileDetails> _studentProfiles = [];
    private readonly Dictionary<Guid, LandlordProfileDetails> _landlordProfiles = [];
    private readonly Dictionary<Guid, UserSettings> _settings = [];

    public StudentProfileDetails GetStudentDetails(Guid userId, string fallbackFullName)
    {
        if (_studentProfiles.TryGetValue(userId, out var existing))
        {
            return existing;
        }

        var seeded = CreateStudentSeed(fallbackFullName);
        _studentProfiles[userId] = seeded;
        return seeded;
    }

    public void SaveStudentDetails(Guid userId, StudentProfileDetails details)
        => _studentProfiles[userId] = details;

    public LandlordProfileDetails GetLandlordDetails(Guid userId, string fallbackFullName)
    {
        if (_landlordProfiles.TryGetValue(userId, out var existing))
        {
            return existing;
        }

        var seeded = CreateLandlordSeed(fallbackFullName);
        _landlordProfiles[userId] = seeded;
        return seeded;
    }

    public void SaveLandlordDetails(Guid userId, LandlordProfileDetails details)
        => _landlordProfiles[userId] = details;

    public UserSettings GetSettings(Guid userId)
    {
        if (_settings.TryGetValue(userId, out var existing))
        {
            return existing;
        }

        var seeded = new UserSettings();
        _settings[userId] = seeded;
        return seeded;
    }

    public void SaveSettings(Guid userId, UserSettings settings)
        => _settings[userId] = settings;

    private static string NormalizeName(string fullName)
        => string.IsNullOrWhiteSpace(fullName) ? "Người dùng" : fullName.Trim();

    private static StudentProfileDetails CreateStudentSeed(string fallbackFullName) => new()
    {
        FullName = NormalizeName(fallbackFullName),
        DateOfBirth = new DateTime(2003, 5, 12),
        Gender = "Nam",
        Phone = "0901 234 567",
        University = "Đại học Bách khoa TP.HCM",
        Major = "Khoa học máy tính",
        AcademicYear = "Năm 3",
        Budget = "3 – 5 triệu/tháng",
        PreferredLocation = "Quận 10, TP.HCM",
        RoomType = "Phòng trọ",
        MoveInDate = DateTime.Today.AddDays(30),
        RentalDuration = "6 – 12 tháng",
        Occupants = "1 người",
        GenderPreference = "Không yêu cầu",
        HasPets = false,
        IsSmoker = false,
        Introduction = "Sinh viên năm 3, tìm phòng yên tĩnh gần trường, ưu tiên an ninh và giờ giấc linh hoạt."
    };

    private static LandlordProfileDetails CreateLandlordSeed(string fallbackFullName) => new()
    {
        FullName = NormalizeName(fallbackFullName),
        Phone = "0987 654 321",
        AccountType = "Cá nhân",
        BusinessName = "Nhà trọ Bình An",
        ContactAddress = "12 Hòa Hảo, Quận 10, TP.HCM",
        Introduction = "Chuyên cho thuê phòng trọ và căn hộ mini gần các trường đại học, hỗ trợ xem phòng trong ngày.",
        PhoneVerification = ProfileVerificationStatus.Verified,
        EmailVerification = ProfileVerificationStatus.Verified,
        IdentityVerification = ProfileVerificationStatus.Pending,
        PropertyOwnershipVerification = ProfileVerificationStatus.Unverified,
        RoomStats = new ProfileRoomStats { Total = 6, Available = 3, Occupied = 2, Pending = 1 },
        Reputation = new ProfileReputation { AverageRating = 4.6, TotalReviews = 28, ResponseRatePercent = 92 }
    };
}
