namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Represents a selectable room type item with standard code and localized display name.
/// </summary>
public sealed record RoomTypeOption(string Code, string DisplayName, string Icon, string Description)
{
    public static readonly IReadOnlyList<RoomTypeOption> DefaultOptions =
    [
        new("BoardingRoom", "Phòng trọ", "🏠", "Phòng trọ truyền thống, dãy trọ sinh viên"),
        new("MiniApartment", "Chung cư mini", "🏢", "Căn hộ mini khép kín, tiện nghi hiện đại"),
        new("WholeHouse", "Nhà nguyên căn", "🏡", "Nhà riêng biệt, phù hợp nhóm bạn cùng ở"),
        new("Dormitory", "Ký túc xá", "🛏️", "Phòng ký túc xá hoặc ở ghép homestay")
    ];
}

/// <summary>
/// Represents a photo selected from the device for the listing creation form.
/// </summary>
public sealed class ListingPhotoItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public bool IsCover { get; set; }

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}

/// <summary>
/// Data transfer object holding validated listing submission payload.
/// </summary>
public sealed record CreateListingRequest(
    string Title,
    string Address,
    decimal MonthlyRentVnd,
    decimal DepositVnd,
    double AreaM2,
    string RoomType,
    string? Description,
    IReadOnlyList<string> PhotoFilePaths);

/// <summary>
/// Result returned after submitting a listing creation request.
/// </summary>
public sealed record CreateListingResult(
    bool IsSuccess,
    string? ListingId = null,
    string? ErrorMessage = null)
{
    public static CreateListingResult Success(string listingId) => new(true, listingId);
    public static CreateListingResult Failure(string errorMessage) => new(false, null, errorMessage);
}
