using System.Globalization;
using RoomToRentRadar.Core.Media;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Favourites.ViewModels;

public sealed record FavouriteRoomItem(
    Guid ListingId,
    string Title,
    string PriceText,
    string AreaText,
    string BedroomsText,
    string Address,
    string StatusText,
    bool IsAvailable,
    string? CoverPhotoUrl)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public string PhotoSource => MediaUrlResolver.Resolve(CoverPhotoUrl) ?? "room_1.png";

    public static FavouriteRoomItem FromDto(FavouriteRoomDto favourite)
        => new(
            favourite.ListingId,
            favourite.Title,
            $"{favourite.Price.ToString("N0", Vietnamese)} đ",
            $"{favourite.AreaM2.ToString("N0", Vietnamese)} m²",
            $"{favourite.Bedrooms} PN",
            favourite.Address,
            StatusTextFor(favourite.Status),
            favourite.Status == ListingStatus.Published,
            favourite.CoverPhotoUrl);

    private static string StatusTextFor(ListingStatus status) => status switch
    {
        ListingStatus.Published => "Còn trống",
        ListingStatus.PendingReview => "Chờ duyệt",
        ListingStatus.Draft => "Bản nháp",
        ListingStatus.Hidden => "Tạm ẩn",
        ListingStatus.Removed => "Đã gỡ",
        _ => status.ToString()
    };
}
