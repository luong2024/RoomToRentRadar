using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.RoomDetail.ViewModels;

public enum RoomDetailState
{
    Initial,
    Loading,
    Content,
    Unavailable,
    NotFound,
    Error
}

public partial class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    public const string ListingIdQueryKey = "id";

    private const int SimilarRoomsLimit = 4;

    private static readonly IReadOnlyDictionary<AmenityCode, string> AmenityLabelsByCode =
        new Dictionary<AmenityCode, string>
        {
            [AmenityCode.Wifi] = "Wi-Fi",
            [AmenityCode.AirConditioner] = "Air conditioning",
            [AmenityCode.Parking] = "Parking",
            [AmenityCode.WashingMachine] = "Washing machine",
            [AmenityCode.Refrigerator] = "Refrigerator",
            [AmenityCode.Kitchen] = "Kitchen",
            [AmenityCode.PrivateBathroom] = "Private bathroom",
            [AmenityCode.Balcony] = "Balcony",
            [AmenityCode.Elevator] = "Elevator",
            [AmenityCode.Security] = "Security",
            [AmenityCode.Gym] = "Gym",
            [AmenityCode.Pool] = "Pool",
            [AmenityCode.PetAllowed] = "Pets allowed"
        };

    private readonly IApiClient _apiClient;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    private Guid? _listingId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingVisible))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsUnavailableVisible))]
    [NotifyPropertyChangedFor(nameof(IsNotFoundVisible))]
    [NotifyPropertyChangedFor(nameof(IsErrorVisible))]
    [NotifyPropertyChangedFor(nameof(IsSignInPromptVisible))]
    public partial RoomDetailState State { get; set; }

    [ObservableProperty]
    public partial ListingDetailDto? Listing { get; set; }

    [ObservableProperty]
    public partial string PostedOnText { get; set; }

    [ObservableProperty]
    public partial string UpdatedOnText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string RoomTypeText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAmenities))]
    public partial IReadOnlyList<string> AmenityLabels { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRules))]
    public partial IReadOnlyList<string> Rules { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPhoneVisible))]
    [NotifyPropertyChangedFor(nameof(IsSignInPromptVisible))]
    public partial string LandlordPhone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimilarRooms))]
    [NotifyPropertyChangedFor(nameof(HasSimilarStatusMessage))]
    public partial bool IsSimilarLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimilarStatusMessage))]
    public partial string SimilarStatusMessage { get; set; }

    public RoomDetailViewModel(IApiClient apiClient, IErrorHandler errorHandler, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        PostedOnText = string.Empty;
        UpdatedOnText = string.Empty;
        StatusText = string.Empty;
        RoomTypeText = string.Empty;
        AmenityLabels = [];
        Rules = [];
        LandlordPhone = string.Empty;
        SimilarStatusMessage = string.Empty;
    }

    public ObservableCollection<ListingSummaryDto> SimilarRooms { get; } = [];

    public bool IsLoadingVisible => State is RoomDetailState.Initial or RoomDetailState.Loading;

    public bool IsContentVisible => State == RoomDetailState.Content;

    public bool IsUnavailableVisible => State == RoomDetailState.Unavailable;

    public bool IsNotFoundVisible => State == RoomDetailState.NotFound;

    public bool IsErrorVisible => State == RoomDetailState.Error;

    public bool HasAmenities => AmenityLabels.Count > 0;

    public bool HasRules => Rules.Count > 0;

    public bool IsPhoneVisible => !string.IsNullOrEmpty(LandlordPhone);

    public bool IsSignInPromptVisible => IsContentVisible && !IsPhoneVisible;

    public bool HasSimilarRooms => SimilarRooms.Count > 0;

    public bool HasSimilarStatusMessage => !string.IsNullOrEmpty(SimilarStatusMessage) && !HasSimilarRooms;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(ListingIdQueryKey, out var value)
            && Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var listingId))
        {
            _listingId = listingId;

            // Shell applies query attributes before OnAppearing, but the order is not
            // guaranteed; start the load here and let OnAppearing act as a fallback.
            if (State == RoomDetailState.Initial)
                LoadCommand.Execute(null);

            return;
        }

        _listingId = null;
        State = RoomDetailState.NotFound;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_listingId is not { } listingId || State == RoomDetailState.Loading)
            return;

        State = RoomDetailState.Loading;

        try
        {
            var result = await _apiClient.GetListingDetailAsync(listingId, cancellationToken);

            switch (result.Outcome)
            {
                case ListingDetailFetchOutcome.NotFound:
                    State = RoomDetailState.NotFound;
                    break;

                case ListingDetailFetchOutcome.Unavailable:
                    State = RoomDetailState.Unavailable;
                    await LoadSimilarRoomsAsync(listingId, cancellationToken);
                    break;

                default:
                    var detail = result.Detail
                        ?? throw new InvalidOperationException("The listing detail response was empty.");
                    ShowDetail(detail);
                    State = RoomDetailState.Content;
                    await RefreshContactAsync(cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            State = RoomDetailState.Error;
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task RefreshContactAsync(CancellationToken cancellationToken)
    {
        if (State != RoomDetailState.Content || _listingId is not { } listingId)
            return;

        try
        {
            var result = await _apiClient.GetListingContactAsync(listingId, cancellationToken);
            LandlordPhone = result.Outcome == ListingContactFetchOutcome.Found
                ? result.Contact?.LandlordPhone ?? string.Empty
                : string.Empty;
        }
        catch (Exception)
        {
            // The contact card falls back to the sign-in prompt; the details stay usable.
            LandlordPhone = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ShowLoginAsync()
    {
        try
        {
            await _navigationService.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenSimilarRoomAsync(ListingSummaryDto listing)
    {
        try
        {
            await _navigationService.GoToRoomDetailAsync(listing.Id);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigationService.GoBackAsync();

    private void ShowDetail(ListingDetailDto detail)
    {
        Listing = detail;
        PostedOnText = FormatDate(detail.CreatedAt);
        UpdatedOnText = FormatDate(detail.UpdatedAt);
        StatusText = detail.Status switch
        {
            ListingStatus.Draft => "Draft",
            ListingStatus.Published => "Available",
            ListingStatus.Hidden => "Hidden",
            ListingStatus.Removed => "Removed",
            _ => detail.Status.ToString()
        };
        RoomTypeText = detail.RoomType switch
        {
            RoomType.Studio => "Studio",
            RoomType.House => "House",
            RoomType.SharedRoom => "Shared room",
            RoomType.SingleRoom => "Single room",
            _ => "Apartment"
        };
        AmenityLabels = detail.Amenities
            .Select(code => AmenityLabelsByCode.TryGetValue(code, out var label) ? label : code.ToString())
            .ToList();
        Rules = detail.Rules.ToList();
        LandlordPhone = string.Empty;
    }

    private async Task LoadSimilarRoomsAsync(Guid listingId, CancellationToken cancellationToken)
    {
        SimilarRooms.Clear();
        SimilarStatusMessage = string.Empty;
        IsSimilarLoading = true;

        try
        {
            var rooms = await _apiClient.GetSimilarListingsAsync(
                listingId,
                SimilarRoomsLimit,
                cancellationToken);

            foreach (var room in rooms)
            {
                SimilarRooms.Add(room);
            }

            SimilarStatusMessage = SimilarRooms.Count == 0
                ? "No similar rooms are available right now."
                : string.Empty;
        }
        catch (Exception)
        {
            SimilarStatusMessage = "Could not load similar rooms.";
        }
        finally
        {
            IsSimilarLoading = false;
            OnPropertyChanged(nameof(HasSimilarRooms));
            OnPropertyChanged(nameof(HasSimilarStatusMessage));
        }
    }

    private static string FormatDate(DateTime value)
        => value.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
