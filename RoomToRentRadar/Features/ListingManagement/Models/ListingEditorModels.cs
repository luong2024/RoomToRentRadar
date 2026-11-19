using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Core.Constants;

namespace RoomToRentRadar.Features.ListingManagement.Models;

public partial class ListingFormField : ObservableObject
{
    [ObservableProperty] public partial string Text { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;
}

public sealed record RoomTypeOption(RoomType Value, string Name);

public sealed record ExistingListingPhoto(ListingPhotoDto Photo)
{
    public string ThumbnailUrl => new Uri(new Uri(ApiConstants.BaseAddress),
        string.IsNullOrWhiteSpace(Photo.ThumbUrl) ? Photo.Url : Photo.ThumbUrl).AbsoluteUri;
}

public sealed record SelectedListingPhoto(string FileName, string ContentType, string Path, IRelayCommand RemoveCommand)
{
    public ListingPhotoUpload ToUpload() => new(FileName, ContentType,
        () => Task.FromResult<Stream>(File.OpenRead(Path)));
}

public sealed record OwnedListingItem(ManagedListingDto Listing)
{
    public Guid Id => Listing.Id;
    public string Title => Listing.Title;
    public string Address => Listing.Address;
    public string StatusText => ListingStatusText.For(Listing.Status);
    public string PriceText => $"{Listing.Price:N0} đ/tháng";
}

public static class ListingStatusText
{
    public static string For(ListingStatus status) => status switch
    {
        ListingStatus.PendingReview => "Chờ duyệt",
        ListingStatus.Published => "Đang đăng",
        ListingStatus.Hidden => "Đã gỡ đăng",
        ListingStatus.Removed => "Đã bị xóa bởi kiểm duyệt",
        _ => status.ToString()
    };
}
