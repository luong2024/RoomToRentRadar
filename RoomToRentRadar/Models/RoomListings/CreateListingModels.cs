namespace RoomToRentRadar.Models.RoomListings;

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
    public string ContentType { get; init; } = "image/jpeg";
    public byte[]? ContentBytes { get; init; }
    public Func<Task<Stream>>? StreamOpener { get; init; }
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

    public async Task<Stream> OpenReadStreamAsync()
    {
        if (StreamOpener != null)
        {
            var stream = await StreamOpener();
            if (stream == null || !stream.CanRead)
            {
                throw new InvalidOperationException($"Không thể mở luồng dữ liệu cho hình ảnh '{FileName}'.");
            }
            return stream;
        }

        if (ContentBytes != null)
        {
            return new MemoryStream(ContentBytes);
        }

        if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
        {
            return File.OpenRead(FilePath);
        }

        throw new FileNotFoundException($"Không tìm thấy file hình ảnh: '{FilePath}'", FilePath);
    }
}

/// <summary>
/// Data transfer object holding validated listing submission payload with actual photo streams.
/// </summary>
public sealed record CreateListingRequest(
    string Title,
    string Address,
    decimal MonthlyRentVnd,
    decimal DepositVnd,
    double AreaM2,
    string RoomType,
    string? Description,
    IReadOnlyList<ListingPhotoItem> Photos,
    IReadOnlyList<string>? PhotoUrls = null);

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
