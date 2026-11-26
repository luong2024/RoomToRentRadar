namespace RoomToRentRadar.Shared.Dtos;

/// <summary>Mã cơ sở (campus) dùng chung API + MAUI, tránh lặp chuỗi "ictu-main".</summary>
public static class CampusIds
{
    public const string IctuMain = "ictu-main";

    /// <summary>Campus mặc định khi request không gửi campusId.</summary>
    public const string Default = IctuMain;

    public static string OrDefault(string? campusId) =>
        string.IsNullOrWhiteSpace(campusId) ? Default : campusId.Trim();
}
