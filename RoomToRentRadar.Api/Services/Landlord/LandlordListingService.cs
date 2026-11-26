using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Photos;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Landlord;

public interface ILandlordListingService
{
    /// <summary>Tạo tin đăng + upload ảnh trong một bước; tin mới luôn ở trạng thái PendingReview.</summary>
    /// <exception cref="RequestValidationException">Dữ liệu hoặc ảnh không hợp lệ.</exception>
    Task<LandlordListingDto> CreateAsync(
        string landlordId,
        CreateListingRequestDto request,
        IReadOnlyList<PhotoUploadItem> photos,
        CancellationToken cancellationToken = default);

    /// <returns>null nếu không tồn tại hoặc không thuộc chủ trọ này.</returns>
    Task<LandlordListingDto?> GetAsync(string landlordId, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// US08 / T08.2 — API đăng tin của chủ trọ.
/// AC1: &gt;= 3 ảnh; AC2: status = PendingReview, isVerified = false đến khi moderator duyệt (T08.3);
/// AC4: chỉ JPG/PNG &lt;= 10MB (tái sử dụng pipeline ảnh T03.1 để nén + tạo thumbnail).
/// </summary>
public sealed class LandlordListingService(
    ILandlordListingRepository landlordRepository,
    IListingRepository listingRepository,
    IPhotoService photoService,
    TimeProvider timeProvider) : ILandlordListingService
{
    private const string ListingIdPrefix = "room-";
    private const int ListingIdRandomLength = 12;
    private const int InitialHouseRulesVersion = 1;

    public async Task<LandlordListingDto> CreateAsync(
        string landlordId,
        CreateListingRequestDto request,
        IReadOnlyList<PhotoUploadItem> photos,
        CancellationToken cancellationToken = default)
    {
        Validate(request, photos);

        var listing = BuildDocument(landlordId, request, timeProvider.GetUtcNow());
        await landlordRepository.AddAsync(listing, cancellationToken);
        await UploadPhotosOrRollbackAsync(listing.Id, photos, cancellationToken);

        var saved = await listingRepository.GetByIdAsync(listing.Id, cancellationToken) ?? listing;
        return LandlordListingMapper.ToDto(saved);
    }

    public async Task<LandlordListingDto?> GetAsync(string landlordId, string id, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(id, cancellationToken);
        return listing is not null && IsOwnedBy(listing, landlordId)
            ? LandlordListingMapper.ToDto(listing)
            : null;
    }

    public static ListingDocument BuildDocument(string landlordId, CreateListingRequestDto request, DateTimeOffset now)
    {
        var campusId = CampusIds.OrDefault(request.CampusId);
        double[] point = [request.Longitude, request.Latitude]; // GeoJSON: [lng, lat]

        return new ListingDocument
        {
            Id = NewListingId(),
            CampusId = campusId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = ListingAvailabilityStatus.PendingReview,
            IsVerified = false,
            ModerationStatus = ListingModerationStatus.PendingReview,
            Rent = request.Rent,
            Deposit = request.Deposit,
            Area = request.Area,
            RoomType = request.RoomType.Trim(),
            Address = request.Address.Trim(),
            Location = new GeoJsonPoint { Coordinates = point },
            DistanceMeters = Math.Round(CampusLocations.DistanceFromCampusMeters(campusId, point)),
            Amenities = ListingCreationRules.NormalizeAmenities(request.Amenities),
            HouseRules = BuildHouseRules(request, now),
            Landlord = BuildLandlord(landlordId, request),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Id ngắn dạng <c>room-xxxxxxxxxxxx</c> (12 ký tự hex ngẫu nhiên).</summary>
    public static string NewListingId() =>
        ListingIdPrefix + Guid.NewGuid().ToString("N")[..ListingIdRandomLength];

    private static void Validate(CreateListingRequestDto request, IReadOnlyList<PhotoUploadItem> photos)
    {
        var errors = ListingCreationRules.Validate(request);
        var photoErrors = ListingCreationRules.ValidatePhotos(photos.Select(p => (p.FileName, p.Length)).ToList());
        foreach (var (field, messages) in photoErrors)
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    /// <summary>Upload thất bại (kể cả lỗi hạ tầng) thì xóa tin vừa tạo để không còn tin thiếu ảnh.</summary>
    private async Task UploadPhotosOrRollbackAsync(
        string listingId,
        IReadOnlyList<PhotoUploadItem> photos,
        CancellationToken cancellationToken)
    {
        try
        {
            await photoService.UploadAsync(listingId, photos, cancellationToken);
        }
        catch (Exception ex)
        {
            await landlordRepository.RemoveAsync(listingId, CancellationToken.None);
            if (ex is PhotoValidationException photoError)
            {
                throw RequestValidationException.For("photos", photoError.Message);
            }

            throw;
        }
    }

    private static bool IsOwnedBy(ListingDocument listing, string landlordId) =>
        string.Equals(listing.Landlord.Id, landlordId, StringComparison.OrdinalIgnoreCase);

    private static HouseRulesDocument BuildHouseRules(CreateListingRequestDto request, DateTimeOffset now) => new()
    {
        Version = InitialHouseRulesVersion,
        UpdatedAt = now,
        QuietHours = TrimOrEmpty(request.QuietHours),
        GuestPolicy = TrimOrEmpty(request.GuestPolicy),
        CookingRules = TrimOrEmpty(request.CookingRules),
        ParkingRules = TrimOrEmpty(request.ParkingRules)
    };

    private static LandlordDocument BuildLandlord(string landlordId, CreateListingRequestDto request) => new()
    {
        Id = landlordId,
        Name = request.ContactName.Trim(),
        Phone = ListingCreationRules.NormalizePhone(request.ContactPhone)
    };

    private static string TrimOrEmpty(string? value) => value?.Trim() ?? string.Empty;
}
