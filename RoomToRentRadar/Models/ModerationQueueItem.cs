using System.Globalization;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Models;

public sealed record ModerationPhotoPreview(string Url);

public sealed class ModerationQueueItem
{
    private readonly ModerationListingDto _sourceListing;

    public ModerationQueueItem(ModerationListingDto listing)
    {
        _sourceListing = listing;
        Id = listing.Id;
        Title = listing.Title;
        Status = listing.Status;
        StatusText = listing.Status == ListingStatus.Published
            ? "Đã duyệt · Đang đăng"
            : "Chờ duyệt";
        CreatedText = listing.Status == ListingStatus.Published
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Đã duyệt lúc {listing.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Gửi lúc {listing.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}");
        PriceText = listing.Price > 0
            ? $"{listing.Price:N0} đ/tháng"
            : "Chưa cập nhật giá";
        DepositText = listing.Deposit > 0
            ? $"Tiền cọc: {listing.Deposit:N0} đ"
            : "Không yêu cầu cọc";
        SpecsText = $"{RoomTypeText.For(listing.RoomType)} · {listing.AreaM2:0.#} m²"
            + (listing.Bedrooms > 0 ? $" · {listing.Bedrooms} PN" : string.Empty);
        LandlordText = string.IsNullOrWhiteSpace(listing.LandlordName)
            ? "Chủ trọ đăng ký"
            : $"Chủ trọ: {listing.LandlordName}"
              + (string.IsNullOrWhiteSpace(listing.LandlordPhone) ? string.Empty : $" · SĐT: {listing.LandlordPhone}");
        Address = string.IsNullOrWhiteSpace(listing.Address)
            ? "Chưa cập nhật địa chỉ"
            : listing.Address;
        Lat = listing.Lat;
        Lng = listing.Lng;
        CoordinatesText = HasCoordinates
            ? string.Create(CultureInfo.InvariantCulture, $"Tọa độ GPS: {Lat:F6}, {Lng:F6}")
            : "Chưa đính kèm tọa độ GPS";
        Description = listing.Description?.Trim() ?? string.Empty;

        RoomTypeBadgeText = RoomTypeText.For(listing.RoomType);
        AreaBadgeText = $"{listing.AreaM2:0.#} m²";
        BedroomsBadgeText = listing.Bedrooms > 0 ? $"{listing.Bedrooms} phòng ngủ" : RoomTypeBadgeText;
        LandlordNameDisplay = string.IsNullOrWhiteSpace(listing.LandlordName)
            ? "Chủ trọ đăng ký trên hệ thống"
            : listing.LandlordName.Trim();
        LandlordPhoneDisplay = string.IsNullOrWhiteSpace(listing.LandlordPhone)
            ? "Chưa cập nhật số điện thoại"
            : $"SĐT liên hệ: {listing.LandlordPhone.Trim()}";
        FallbackHeroIllustration = (((uint)listing.Id.GetHashCode()) % 3) switch
        {
            0 => "room_1.png",
            1 => "room_2.png",
            _ => "room_3.png"
        };

        var orderedPhotos = (listing.Photos ?? [])
            .OrderByDescending(p => p.IsCover)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToArray();

        Photos = orderedPhotos
            .Select(p => MediaUrlResolver.Resolve(string.IsNullOrWhiteSpace(p.ThumbUrl) ? p.Url : p.ThumbUrl))
            .Where(url => !string.IsNullOrEmpty(url))
            .Select(url => new ModerationPhotoPreview(url!))
            .ToArray();

        var firstPhoto = orderedPhotos.FirstOrDefault();
        CoverPhotoUrl = firstPhoto is null
            ? null
            : MediaUrlResolver.Resolve(string.IsNullOrWhiteSpace(firstPhoto.Url) ? firstPhoto.ThumbUrl : firstPhoto.Url)
              ?? Photos.FirstOrDefault()?.Url;
    }

    public Guid Id { get; }
    public string Title { get; }
    public ListingStatus Status { get; }
    public bool IsPending => Status == ListingStatus.PendingReview;
    public bool IsPublished => Status == ListingStatus.Published;
    public string StatusText { get; }
    public string CreatedText { get; }
    public string PriceText { get; }
    public string DepositText { get; }
    public string SpecsText { get; }
    public string RoomTypeBadgeText { get; }
    public string AreaBadgeText { get; }
    public string BedroomsBadgeText { get; }
    public string LandlordText { get; }
    public string LandlordNameDisplay { get; }
    public string LandlordPhoneDisplay { get; }
    public string FallbackHeroIllustration { get; }
    public string Address { get; }
    public double Lat { get; }
    public double Lng { get; }
    public bool HasCoordinates => Lat != 0 || Lng != 0;
    public string CoordinatesText { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public IReadOnlyList<ModerationPhotoPreview> Photos { get; }
    public string? CoverPhotoUrl { get; }
    public string? HeroDisplayUrl => Photos.Count > 0 ? Photos[0].Url : CoverPhotoUrl;
    public string? SecondaryPhoto1Url => Photos.Count > 1 ? Photos[1].Url : null;
    public bool HasSecondaryPhoto1 => SecondaryPhoto1Url is not null;
    public string? SecondaryPhoto2Url => Photos.Count > 2 ? Photos[2].Url : null;
    public bool HasSecondaryPhoto2 => SecondaryPhoto2Url is not null;
    public string? SecondaryPhoto3Url => Photos.Count > 3 ? Photos[3].Url : null;
    public bool HasSecondaryPhoto3 => SecondaryPhoto3Url is not null;
    public bool HasExtraPhotos => Photos.Count > 4;
    public string ExtraPhotosText => Photos.Count > 4 ? $"+{Photos.Count - 4} ảnh" : string.Empty;
    public bool HasPhotos => !string.IsNullOrEmpty(CoverPhotoUrl);
    public bool MissingPhotos => string.IsNullOrEmpty(CoverPhotoUrl);
    public bool HasMultiplePhotos => Photos.Count > 1;
    public bool HasSinglePhotoOrNone => !HasMultiplePhotos;
    public bool HasMoreThanThreePhotos => Photos.Count > 3;
    public string MoreThanThreePhotosText => Photos.Count > 3 ? $"+{Photos.Count - 3} ảnh" : string.Empty;
    public string PhotoCountText => $"{Photos.Count} ảnh";

    public ModerationQueueItem ToApprovedSnapshot(DateTime approvedAtUtc)
        => new(_sourceListing with
        {
            Status = ListingStatus.Published,
            UpdatedAt = approvedAtUtc
        });
}
