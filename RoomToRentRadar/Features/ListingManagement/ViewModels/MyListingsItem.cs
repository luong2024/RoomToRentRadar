using CommunityToolkit.Mvvm.ComponentModel;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

/// <summary>Display projection of a <see cref="ManagedListingDto"/> for "Tin của tôi".</summary>
public sealed partial class MyListingsItem : ObservableObject
{
    private static readonly System.Globalization.CultureInfo Vietnamese =
        System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    public MyListingsItem(ManagedListingDto listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        Id = listing.Id;
        Illustration = $"room_{listing.Id.ToByteArray()[0] % 3 + 1}.png";
        Apply(listing);
    }

    public Guid Id { get; }

    /// <summary>Local illustration used when the listing has no photo URL.</summary>
    public string Illustration { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ListingStatus Status { get; set; }

    /// <summary>Absolute cover photo URI, or null when the listing has no photos.</summary>
    [ObservableProperty]
    public partial string? CoverImageSource { get; set; }

    /// <summary>Illustration shown while the listing has no real photo.</summary>
    public string PhotoSource => CoverImageSource ?? Illustration;

    public bool HasCoverPhoto => CoverImageSource is not null;

    /// <summary>True when the card shows the bundled illustration instead of a photo.</summary>
    public bool ShowIllustrationNote => !HasCoverPhoto;

    /// <summary>Cleared while the page is busy so per-item actions cannot race.</summary>
    [ObservableProperty]
    public partial bool IsActionEnabled { get; set; } = true;

    /// <summary>Only published and pending listings can be hidden (US08 AC3).</summary>
    public bool CanUnpublish => Status is ListingStatus.Published or ListingStatus.PendingReview;

    public static MyListingsItem FromDto(ManagedListingDto listing) => new(listing);

    /// <summary>Refreshes the projection after the listing was edited or hidden.</summary>
    public void Apply(ManagedListingDto listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        Title = listing.Title;
        PriceText = $"{listing.Price.ToString("N0", Vietnamese)} đ";
        AreaText = $"{listing.AreaM2.ToString("N0", Vietnamese)} m²";
        Address = listing.Address;
        Status = listing.Status;
        StatusText = DescribeStatus(listing.Status);

        var photo = listing.Photos
            .FirstOrDefault(candidate => candidate.IsCover)
            ?? listing.Photos.FirstOrDefault();

        CoverImageSource = ListingPhotoUrl.Resolve(photo?.ThumbUrl) ?? ListingPhotoUrl.Resolve(photo?.Url);

        OnPropertyChanged(nameof(PhotoSource));
        OnPropertyChanged(nameof(HasCoverPhoto));
        OnPropertyChanged(nameof(ShowIllustrationNote));
        OnPropertyChanged(nameof(CanUnpublish));
    }

    public static string DescribeStatus(ListingStatus status) => status switch
    {
        ListingStatus.Draft => "Nháp",
        ListingStatus.PendingReview => "Chờ duyệt",
        ListingStatus.Published => "Đã đăng",
        ListingStatus.Hidden => "Đã ẩn",
        ListingStatus.Removed => "Đã gỡ",
        _ => "Không rõ"
    };
}
