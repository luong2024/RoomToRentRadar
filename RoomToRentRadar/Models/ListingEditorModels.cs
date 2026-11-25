using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Models;

public partial class ListingFormField : ObservableObject
{
    [ObservableProperty] public partial string Text { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;
}

public sealed record RoomTypeOption(RoomType Value, string Name);

public sealed record ExistingListingPhoto(ListingPhotoDto Photo)
{
    public string ThumbnailUrl => MediaUrlResolver.Resolve(
        string.IsNullOrWhiteSpace(Photo.ThumbUrl) ? Photo.Url : Photo.ThumbUrl) ?? string.Empty;
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
    public ListingStatus Status => Listing.Status;
    public string StatusText => ListingStatusText.For(Listing.Status);
    public string PriceText => $"{Listing.Price:N0} đ/tháng";
    public string RoomSummaryText => $"{RoomTypeText.For(Listing.RoomType)} · {Listing.AreaM2:0.#} m²";
    public string? CoverThumbnailUrl
    {
        get
        {
            var cover = Listing.Photos?
                .OrderByDescending(p => p.IsCover)
                .ThenBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .FirstOrDefault();
            return cover is null
                ? null
                : MediaUrlResolver.Resolve(string.IsNullOrWhiteSpace(cover.ThumbUrl) ? cover.Url : cover.ThumbUrl);
        }
    }

    public bool HasCoverThumbnail => !string.IsNullOrEmpty(CoverThumbnailUrl);
    public bool MissingCoverThumbnail => string.IsNullOrEmpty(CoverThumbnailUrl);
    public string FallbackIllustration => (((uint)Listing.Id.GetHashCode()) % 3) switch
    {
        0 => "room_1.png",
        1 => "room_2.png",
        _ => "room_3.png"
    };
    public bool IsPublished => Listing.Status == ListingStatus.Published;
    public bool IsPendingReview => Listing.Status == ListingStatus.PendingReview;
    public bool CanEdit => Listing.Status != ListingStatus.Removed;
    public bool CanUnpublish => Listing.Status is ListingStatus.PendingReview or ListingStatus.Published;
    public bool CanPublish => Listing.Status == ListingStatus.Hidden;
}

public partial class OwnedListingFilterOption : ObservableObject
{
    public OwnedListingFilterOption(ListingStatus? status, string baseLabel, Action<ListingStatus?> onSelect)
    {
        ArgumentNullException.ThrowIfNull(baseLabel);
        ArgumentNullException.ThrowIfNull(onSelect);
        Status = status;
        BaseLabel = baseLabel;
        SelectCommand = new RelayCommand(() => onSelect(Status));
    }

    public ListingStatus? Status { get; }
    public string BaseLabel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string DisplayText => $"{BaseLabel} ({Count})";
    public IRelayCommand SelectCommand { get; }
}

public static class RoomTypeText
{
    public static string For(RoomType roomType) => roomType switch
    {
        RoomType.SingleRoom => "Phòng riêng",
        RoomType.SharedRoom => "Phòng ở ghép",
        RoomType.Studio => "Studio",
        RoomType.Apartment => "Căn hộ",
        RoomType.House => "Nhà nguyên căn",
        _ => roomType.ToString()
    };
}

public static class ListingStatusText
{
    public static string For(ListingStatus status) => status switch
    {
        ListingStatus.PendingReview => "Chờ duyệt",
        ListingStatus.Published => "Đã duyệt · Hoàn thành",
        ListingStatus.Hidden => "Đã gỡ đăng",
        ListingStatus.Removed => "Đã bị xóa bởi kiểm duyệt",
        _ => status.ToString()
    };
}
