using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services.Profile;

public interface IProfileDetailsStore
{
    StudentProfileDetails GetStudentDetails(Guid userId, string fallbackFullName);

    void SaveStudentDetails(Guid userId, StudentProfileDetails details);

    LandlordProfileDetails GetLandlordDetails(Guid userId, string fallbackFullName);

    void SaveLandlordDetails(Guid userId, LandlordProfileDetails details);

    UserSettings GetSettings(Guid userId);

    void SaveSettings(Guid userId, UserSettings settings);
}
