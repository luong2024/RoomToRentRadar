using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Photos;
using RoomToRentRadar.Shared.Dtos;
using SharedPhotoDto = RoomToRentRadar.Shared.Photos.PhotoDto;

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
    public async Task<LandlordListingDto> CreateAsync(
        string landlordId,
        CreateListingRequestDto request,
        IReadOnlyList<PhotoUploadItem> photos,
        CancellationToken cancellationToken = default)
    {
        var errors = ListingCreationRules.Validate(request);
        foreach (var (key, value) in ListingCreationRules.ValidatePhotos(photos.Select(p => (p.FileName, p.Length)).ToList()))
        {
            errors[key] = value;
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var listing = BuildDocument(landlordId, request, timeProvider.GetUtcNow());
        await landlordRepository.AddAsync(listing, cancellationToken);

        try
        {
            await photoService.UploadAsync(listing.Id, photos, cancellationToken);
        }
        catch (PhotoValidationException ex)
        {
            await landlordRepository.RemoveAsync(listing.Id, CancellationToken.None);
            throw RequestValidationException.For("photos", ex.Message);
        }
        catch
        {
            await landlordRepository.RemoveAsync(listing.Id, CancellationToken.None);
            throw;
        }

        var saved = await listingRepository.GetByIdAsync(listing.Id, cancellationToken) ?? listing;
        return ToDto(saved);
    }

    public async Task<LandlordListingDto?> GetAsync(string landlordId, string id, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(id, cancellationToken);
        return listing is not null && string.Equals(listing.Landlord.Id, landlordId, StringComparison.OrdinalIgnoreCase)
            ? ToDto(listing)
            : null;
    }

    public static ListingDocument BuildDocument(string landlordId, CreateListingRequestDto request, DateTimeOffset now)
    {
        var campusId = string.IsNullOrWhiteSpace(request.CampusId) ? "ictu-main" : request.CampusId.Trim();
        double[] point = [request.Longitude, request.Latitude]; // GeoJSON: [lng, lat]

        return new ListingDocument
        {
            Id = $"room-{Guid.NewGuid():N}"[..17],
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
            DistanceMeters = Math.Round(CampusLocations.HaversineMeters(CampusLocations.Get(campusId), point)),
            Amenities = ListingCreationRules.NormalizeAmenities(request.Amenities),
            HouseRules = new HouseRulesDocument
            {
                Version = 1,
                UpdatedAt = now,
                QuietHours = request.QuietHours?.Trim() ?? string.Empty,
                GuestPolicy = request.GuestPolicy?.Trim() ?? string.Empty,
                CookingRules = request.CookingRules?.Trim() ?? string.Empty,
                ParkingRules = request.ParkingRules?.Trim() ?? string.Empty
            },
            Landlord = new LandlordDocument
            {
                Id = landlordId,
                Name = request.ContactName.Trim(),
                Phone = ListingCreationRules.NormalizePhone(request.ContactPhone)
            },
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static LandlordListingDto ToDto(ListingDocument l)
    {
        var photos = l.Photos.OrderBy(p => p.Order).Select(p => new SharedPhotoDto
        {
            Id = p.Id,
            Url = p.Url,
            ThumbnailUrl = p.ThumbnailUrl,
            Is360 = p.Is360,
            Order = p.Order,
            Width = p.Width,
            Height = p.Height,
            SizeBytes = p.SizeBytes
        }).ToList();

        return new LandlordListingDto
        {
            Id = l.Id,
            CampusId = l.CampusId,
            Title = l.Title,
            Description = l.Description,
            Status = l.Status,
            StatusLabel = ListingStatusLabels.ToLabel(l.Status),
            ModerationStatus = l.ModerationStatus,
            IsVerified = l.IsVerified,
            Rent = l.Rent,
            Deposit = l.Deposit,
            Area = l.Area,
            RoomType = l.RoomType,
            Address = l.Address,
            Longitude = l.Location.Coordinates.ElementAtOrDefault(0),
            Latitude = l.Location.Coordinates.ElementAtOrDefault(1),
            DistanceMeters = l.DistanceMeters,
            Amenities = [.. l.Amenities],
            ContactName = l.Landlord.Name,
            ContactPhone = l.Landlord.Phone,
            PhotoCount = photos.Count,
            CoverPhotoUrl = photos.Select(p => p.ThumbnailUrl is { Length: > 0 } t ? t : p.Url).FirstOrDefault() ?? string.Empty,
            Photos = photos,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        };
    }
}
