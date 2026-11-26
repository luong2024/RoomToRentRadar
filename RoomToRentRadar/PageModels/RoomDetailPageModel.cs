using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fonts;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Converters;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Pages.Controls.RoomDetail.Models;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// Presentation PageModel for SCR-ROOM-01 (Room Detail) and SCR-ROOM-01-UNAVAILABLE (Unavailable Listing Handling).
/// Implements AC1-AC4 of US02, BR-LST-03, BR-LST-04a, BR-LST-08, BR-LST-10, BR-AUTH-07, BR-HR-01, BR-CNT-01.
/// </summary>
#if !UNIT_TESTS
[QueryProperty(nameof(RoomId), "roomId")]
[QueryProperty(nameof(CampusId), "campusId")]
#endif
public partial class RoomDetailPageModel : ObservableObject, IDisposable
{
    private const int RelativeDateLimitDays = 7;

    private readonly IRoomGateway _roomGateway;
    private readonly ISessionStore _sessionStore;
    private readonly IFavouriteGateway? _favouriteGateway;
    private readonly PropertyChangedEventHandler _cultureChangedHandler;
    private bool _disposed;

    private readonly List<AmenityDisplayItem> _allAmenities = [];
    private TravelTimesDto? _lastTravelTimes;

    #region Query & Navigation Properties

    [ObservableProperty]
    public partial string RoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CampusId { get; set; }

    [ObservableProperty]
    public partial string CampusName { get; set; } = "ICTU";

    #endregion

    #region State & Visibility Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    public partial bool HasError { get; set; } = false;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    public partial bool IsNotFound { get; set; } = false;

    public bool IsContentVisible => !IsLoading && !HasError && !IsNotFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnavailableDebugPreview))]
    [NotifyPropertyChangedFor(nameof(ShowDescription))]
    public partial bool IsAvailable { get; set; } = true;

    [ObservableProperty]
    public partial bool IsUnavailable { get; set; } = false;

#if DEBUG
    public bool IsDebugMode => true;
#else
    public bool IsDebugMode => false;
#endif

    public bool ShowUnavailableDebugPreview => IsDebugMode && IsAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    public partial bool IsSignedIn { get; set; } = false;

    public bool IsGuest => !IsSignedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavouriteIconGlyph))]
    public partial bool IsFavourite { get; set; } = false;

    public string FavouriteIconGlyph => FluentUI.heart_24_regular;

    [ObservableProperty]
    public partial RoomDetailDto? Room { get; set; }

    #endregion

    #region Hero Gallery Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPhotoIndexZeroBased))]
    public partial int CurrentPhotoIndex { get; set; } = 1;

    public int CurrentPhotoIndexZeroBased
    {
        get => Math.Max(0, CurrentPhotoIndex - 1);
        set
        {
            int oneBased = Photos.Count == 0
                ? 1
                : Math.Clamp(value + 1, 1, Photos.Count);
            if (CurrentPhotoIndex != oneBased)
            {
                CurrentPhotoIndex = oneBased;
            }
        }
    }

    partial void OnCurrentPhotoIndexChanged(int value)
    {
        PhotoCounterText = $"{value} / {TotalPhotosCount}";
    }

    [ObservableProperty]
    public partial int TotalPhotosCount { get; set; } = 1;

    [ObservableProperty]
    public partial string PhotoCounterText { get; set; } = "1 / 1";

    [ObservableProperty]
    public partial string? HeroCoverUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPhotos))]
    public partial bool HasPhotos { get; set; } = false;

    public bool HasNoPhotos => !HasPhotos;

    [ObservableProperty]
    public partial bool Is360Available { get; set; } = false;

    public ObservableCollection<PhotoDto> Photos { get; } = [];

    #endregion

    #region Title, Price & Deposit Properties

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    [NotifyPropertyChangedFor(nameof(ShowDescription))]
    public partial string Description { get; set; } = string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool ShowDescription => IsAvailable && HasDescription;

    [ObservableProperty]
    public partial string PriceFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PeriodFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DepositFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DepositShortFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceShortFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusBadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdatedAgoText { get; set; } = string.Empty;

    #endregion

    #region Key Facts & Monthly Costs Collections

    public ObservableCollection<KeyFactItem> KeyFacts { get; } = [];

    public ObservableCollection<KeyFactRowItem> KeyFactRows { get; } = [];

    public ObservableCollection<CostRowItem> CostBreakdownRows { get; } = [];

    [ObservableProperty]
    public partial string EstimatedTotalFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EstimateNoteFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OtherFeesNoteFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOtherFeesNote { get; set; }

    #endregion

    #region Amenities Properties

    public ObservableCollection<AmenityDisplayItem> AmenitiesDisplayList { get; } = [];

    public ObservableCollection<AmenityRowItem> AmenityRows { get; } = [];

    [ObservableProperty]
    public partial bool IsAllAmenitiesExpanded { get; set; } = false;

    [ObservableProperty]
    public partial bool HasMoreAmenities { get; set; } = false;

    [ObservableProperty]
    public partial string SeeAllAmenitiesText { get; set; } = string.Empty;

    #endregion

    #region Safety Properties

    [ObservableProperty]
    public partial string SafetyScoreFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSafetyVerified { get; set; } = false;

    public ObservableCollection<SafetyCheckItem> SafetyChecklist { get; } = [];

    #endregion

    #region House Rules Properties

    public ObservableCollection<HouseRuleItem> HouseRulesList { get; } = [];

    [ObservableProperty]
    public partial bool HasHouseRules { get; set; } = false;

    #endregion

    #region Location & Commute Properties

    [ObservableProperty]
    public partial string AddressFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CommuteHeaderFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StraightLineNotice { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCommuteFallback { get; set; } = false;

    [ObservableProperty]
    public partial bool HasTravelTimes { get; set; } = false;

    [ObservableProperty]
    public partial double Latitude { get; set; } = 21.5851;

    [ObservableProperty]
    public partial double Longitude { get; set; } = 105.8066;

    public ObservableCollection<CommuteModeItem> CommuteTimes { get; } = [];

    #endregion

    #region Landlord Properties

    [ObservableProperty]
    public partial string LandlordInitials { get; set; } = "LL";

    [ObservableProperty]
    public partial string LandlordName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LandlordBadge { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? LandlordPhoneNumber { get; set; }

    [ObservableProperty]
    public partial string? LandlordZaloUrl { get; set; }

    [ObservableProperty]
    public partial bool CanCall { get; set; } = false;

    [ObservableProperty]
    public partial bool CanZalo { get; set; } = false;

    #endregion

    #region Unavailable State & Similar Rooms

    [ObservableProperty]
    public partial string UnavailableBannerText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnavailableSummaryFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContactDisabledText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSimilarRooms { get; set; } = false;

    public ObservableCollection<SimilarRoomDisplayModel> SimilarRooms { get; } = [];

    #endregion

    public RoomDetailPageModel(
        IRoomGateway roomGateway,
        ISessionStore sessionStore,
        IFavouriteGateway? favouriteGateway = null)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _favouriteGateway = favouriteGateway;

        _cultureChangedHandler = (_, _) => OnCultureChanged();
        LocalizedStrings.Instance.PropertyChanged += _cultureChangedHandler;
    }

    #region Initialization & Data Loading

    [RelayCommand]
    public Task InitializeAsync() => LoadRoomAsync(showSkeleton: true);

    [RelayCommand]
    public Task RefreshAsync() => LoadRoomAsync(showSkeleton: false);

    private async Task LoadRoomAsync(bool showSkeleton)
    {
        if (string.IsNullOrWhiteSpace(RoomId))
        {
            IsLoading = false;
            IsBusy = false;
            IsNotFound = true;
            return;
        }

        try
        {
            if (showSkeleton)
            {
                IsLoading = true;
            }
            else
            {
                IsBusy = true;
            }
            HasError = false;
            IsNotFound = false;
            ErrorMessage = string.Empty;
            _lastTravelTimes = null;

            // Check authentication state for BR-AUTH-07 / BR-CNT-01
            IsSignedIn = await _sessionStore.HasValidTokenAsync();

            // Load full room details via IRoomGateway
            var detail = await _roomGateway.GetRoomDetailAsync(RoomId, CampusId);
            Room = detail;

            // Populate UI sections
            PopulateRoomDetails(detail);

            // If room is unavailable, load similar rooms recommendation (AC3 / BR-LST-10)
            if (IsUnavailable)
            {
                await LoadSimilarRoomsAsync();
            }

            // Load travel times estimates (US09 integration)
            await LoadTravelTimesAsync();
        }
        catch (ApiException apiEx) when (apiEx.StatusCode == HttpStatusCode.NotFound || apiEx.ErrorCode == ProblemCodes.RoomNotFound)
        {
            IsNotFound = true;
            ErrorMessage = AppResources.GetString("RoomDetail_NotFoundMessage");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ErrorMessageMapper.Map(ex, null);
        }
        finally
        {
            if (showSkeleton)
            {
                IsLoading = false;
            }
            else
            {
                IsBusy = false;
            }
        }
    }

    private void PopulateRoomDetails(RoomDetailDto detail)
    {
        Title = detail.Title;
        Description = detail.Description?.Trim() ?? string.Empty;
        IsFavourite = detail.IsFavourite;
        Latitude = detail.Location.Latitude;
        Longitude = detail.Location.Longitude;

        // Availability state mapping (BR-LST-04a)
        IsAvailable = detail.Status == ListingStatus.Available;
        IsUnavailable = detail.Status is ListingStatus.Rented or ListingStatus.Unpublished;
        if (!IsUnavailable)
        {
            SimilarRooms.Clear();
            HasSimilarRooms = false;
        }

        // Pricing formatting: the header matches the room card; the cost table below keeps full amounts (ui-ux-spec §6)
        var culture = CultureInfo.CurrentCulture;
        PriceFormatted = CultureFormatting.FormatCompactMoney(detail.MonthlyRentVnd, culture);
        PeriodFormatted = AppResources.GetString("RoomDetail_PerMonth");
        PriceShortFormatted = CultureFormatting.FormatMonthlyPrice(detail.MonthlyRentVnd, culture);
        UnavailableSummaryFormatted = $"{PriceShortFormatted} · {CultureFormatting.FormatArea(detail.AreaM2, culture)} · {FormatRoomType(detail.RoomType)}";

        int depositMonths = detail.MonthlyRentVnd > 0 ? (int)Math.Round((double)detail.DepositVnd / detail.MonthlyRentVnd) : 1;
        string monthWord = depositMonths <= 1
            ? AppResources.GetString("RoomDetail_DepositMonth")
            : AppResources.GetString("RoomDetail_DepositMonths");
        string depositPeriod = string.Format(CultureInfo.CurrentCulture, monthWord, depositMonths);
        var deposit = CultureFormatting.FormatDepositMoney(detail.DepositVnd, culture);
        DepositFormatted = string.Format(culture, AppResources.GetString("RoomDetail_Deposit"), deposit, depositPeriod);
        DepositShortFormatted = string.Format(culture, AppResources.GetString("RoomDetail_DepositShort"), deposit);

        // Status badge & relative updated text
        StatusBadgeText = detail.Status switch
        {
            ListingStatus.Available when detail.IsVerified => AppResources.GetString("RoomDetail_VerifiedAvailable"),
            ListingStatus.Available => AppResources.GetString("RoomDetail_StatusAvailable"),
            ListingStatus.Rented => AppResources.GetString("RoomDetail_StatusRented"),
            ListingStatus.Unpublished => AppResources.GetString("RoomDetail_StatusUnpublished"),
            _ => AppResources.GetString("RoomDetail_StatusRented")
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int daysAgo = Math.Max(0, today.DayNumber - DateOnly.FromDateTime(detail.LastUpdatedAtUtc).DayNumber);
        // ui-ux-spec §6: relative for up to seven days, a date after that.
        var updatedOn = daysAgo > RelativeDateLimitDays
            ? CultureFormatting.FormatDate(detail.LastUpdatedAtUtc, culture, today)
            : null;
        UpdatedAgoText = daysAgo switch
        {
            0 => AppResources.GetString("RoomDetail_UpdatedToday"),
            1 => AppResources.GetString("RoomDetail_UpdatedYesterday"),
            _ when updatedOn is not null => string.Format(culture, AppResources.GetString("RoomDetail_UpdatedOn"), updatedOn),
            _ => string.Format(culture, AppResources.GetString("RoomDetail_UpdatedDaysAgo"), daysAgo)
        };

        // Unavailable banner text (SCR-ROOM-01-UNAVAILABLE)
        string relativeTime = daysAgo switch
        {
            0 => AppResources.GetString("RoomDetail_RelativeToday"),
            1 => AppResources.GetString("RoomDetail_RelativeYesterday"),
            _ when updatedOn is not null => string.Format(culture, AppResources.GetString("RoomDetail_RelativeOnDate"), updatedOn),
            _ => string.Format(culture, AppResources.GetString("RoomDetail_RelativeDaysAgo"), daysAgo)
        };
        UnavailableBannerText = detail.Status == ListingStatus.Unpublished
            ? AppResources.GetString("RoomDetail_UnavailableBannerGeneral")
            : string.Format(AppResources.GetString("RoomDetail_UnavailableBanner"), relativeTime);
        ContactDisabledText = AppResources.GetString("RoomDetail_ContactDisabled");

        // Photos gallery setup (sorted by SortOrder so carousel starts on cover)
        Photos.Clear();
        var orderedPhotos = detail.Photos.OrderBy(p => p.SortOrder).ToList();
        foreach (var photo in orderedPhotos)
        {
            Photos.Add(photo);
        }
        TotalPhotosCount = Math.Max(1, orderedPhotos.Count);
        CurrentPhotoIndex = 1;
        PhotoCounterText = $"{CurrentPhotoIndex} / {TotalPhotosCount}";
        HasPhotos = orderedPhotos.Count > 0;
        HeroCoverUrl = orderedPhotos.FirstOrDefault()?.OriginalUrl
            ?? orderedPhotos.FirstOrDefault()?.MediumUrl;
        Is360Available = orderedPhotos.Any(p => p.Kind == PhotoKind.Panorama);

        // Resolve campus label before building Key Facts and Commute sections (BR-CMP-02)
        CampusName = !string.IsNullOrWhiteSpace(CampusId) ? CampusId : "ICTU";

        // Key Facts Grid (2x3)
        PopulateKeyFacts(detail);

        // Monthly Costs Card (BR-LST-03)
        PopulateMonthlyCosts(detail);

        // Amenities Section
        PopulateAmenities(detail.Amenities);

        // Safety Checklist Section (BR-LST-08)
        PopulateSafety(detail.Safety);

        // House Rules Section (BR-HR-01)
        PopulateHouseRules(detail.HouseRules);

        // Address & Commute
        AddressFormatted = $"{detail.Address.AddressLine}, {detail.Address.Ward}, {detail.Address.District}";
        UpdateCommuteNotices(detail.DistanceKm);

        // Landlord contact block
        PopulateLandlord(detail.Landlord);
    }

    private void PopulateKeyFacts(RoomDetailDto detail)
    {
        KeyFacts.Clear();

        // 1. Area
        KeyFacts.Add(new KeyFactItem(
            FluentUI.ruler_24_regular,
            AppResources.GetString("RoomDetail_Area"),
            CultureFormatting.FormatArea(detail.AreaM2, CultureInfo.CurrentCulture)));

        // 2. Room Type
        KeyFacts.Add(new KeyFactItem(
            FluentUI.home_24_regular,
            AppResources.GetString("RoomDetail_RoomType"),
            FormatRoomType(detail.RoomType)));

        // 3. Max Occupants
        string occupantsValue = detail.MaxOccupants is > 0
            ? string.Format(AppResources.GetString("RoomDetail_PeopleCount"), detail.MaxOccupants)
            : AppResources.GetString("RoomDetail_OnePerson");
        KeyFacts.Add(new KeyFactItem(
            FluentUI.people_24_regular,
            AppResources.GetString("RoomDetail_MaxOccupants"),
            occupantsValue));

        // 4. Furnishing
        string furnishingString = detail.Furnishing switch
        {
            Furnishing.Full => AppResources.GetString("Furnishing_Full"),
            Furnishing.Basic => AppResources.GetString("Furnishing_Partial"),
            _ => AppResources.GetString("Furnishing_None")
        };
        KeyFacts.Add(new KeyFactItem(
            FluentUI.bed_24_regular,
            AppResources.GetString("RoomDetail_Furnishing"),
            furnishingString));

        // 5. Available From
        string availableFromText = detail.AvailableFrom.HasValue
            ? CultureFormatting.FormatDate(detail.AvailableFrom.Value, CultureInfo.CurrentCulture)
            : AppResources.GetString("RoomDetail_StatusAvailable");
        KeyFacts.Add(new KeyFactItem(
            FluentUI.calendar_ltr_24_regular,
            AppResources.GetString("RoomDetail_AvailableFrom"),
            availableFromText));

        // 6. Distance to Campus
        KeyFacts.Add(new KeyFactItem(
            FluentUI.location_24_regular,
            string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_ToCampus"), CampusName),
            $"{detail.DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)} km"));

        UpdateKeyFactRows();
    }

    private void UpdateKeyFactRows()
    {
        KeyFactRows.Clear();
        for (int i = 0; i < KeyFacts.Count; i += 3)
        {
            var first = KeyFacts[i];
            var second = i + 1 < KeyFacts.Count ? KeyFacts[i + 1] : null;
            var third = i + 2 < KeyFacts.Count ? KeyFacts[i + 2] : null;
            KeyFactRows.Add(new KeyFactRowItem(first, second, third));
        }
    }

    private static string FormatRoomType(RoomType roomType) => roomType switch
    {
        RoomType.BoardingRoom => AppResources.GetString("RoomType_BoardingRoom"),
        RoomType.MiniApartment => AppResources.GetString("RoomType_MiniApartment"),
        RoomType.WholeHouse => AppResources.GetString("RoomType_WholeHouse"),
        RoomType.Dormitory => AppResources.GetString("RoomType_Dormitory"),
        _ => roomType.ToString()
    };

    private void PopulateMonthlyCosts(RoomDetailDto detail)
    {
        CostBreakdownRows.Clear();
        var utils = detail.Utilities;

        // Rent
        CostBreakdownRows.Add(new CostRowItem(
            AppResources.GetString("RoomDetail_Rent"),
            FullMoney(detail.MonthlyRentVnd)));

        // Electricity
        string elecValue = utils.ElectricityPriceVndPerKwh.HasValue
            ? string.Format(AppResources.GetString("RoomDetail_PerKwh"), FullMoney(utils.ElectricityPriceVndPerKwh.Value))
            : AppResources.GetString("RoomDetail_Included");
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_Electricity"), elecValue));

        // Water
        string waterValue;
        if (!utils.WaterPriceVnd.HasValue || utils.WaterPriceUnit == WaterPriceUnit.Included)
        {
            waterValue = AppResources.GetString("RoomDetail_Included");
        }
        else if (utils.WaterPriceUnit == WaterPriceUnit.PerPerson)
        {
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerPerson"), FullMoney(utils.WaterPriceVnd.Value));
        }
        else
        {
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerM3"), FullMoney(utils.WaterPriceVnd.Value));
        }
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_Water"), waterValue));

        // Internet
        string internetValue = utils.InternetFeeVnd.HasValue && utils.InternetFeeVnd.Value > 0
            ? FullMoney(utils.InternetFeeVnd.Value)
            : AppResources.GetString("RoomDetail_Included");
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_Internet"), internetValue));

        // Motorbike Parking
        string parkingValue = utils.ParkingFeeVnd.HasValue && utils.ParkingFeeVnd.Value > 0
            ? FullMoney(utils.ParkingFeeVnd.Value)
            : AppResources.GetString("RoomDetail_Free");
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_MotorbikeParking"), parkingValue));

        // Total & Other Fees Note (BR-LST-03)
        EstimatedTotalFormatted = string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_EstimatedMonthlyFormat"), CultureFormatting.FormatCompactMoney(utils.EstimatedMonthlyTotalVnd, CultureInfo.CurrentCulture));
        EstimateNoteFormatted = AppResources.GetString("RoomDetail_EstimateNote");
        HasOtherFeesNote = !string.IsNullOrWhiteSpace(utils.OtherFeesNote);
        OtherFeesNoteFormatted = HasOtherFeesNote
            ? string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_OtherFeesFormat"), utils.OtherFeesNote!.Trim())
            : string.Empty;
    }

    private void PopulateAmenities(IReadOnlyList<Amenity> amenities)
    {
        _allAmenities.Clear();
        AmenitiesDisplayList.Clear();

        foreach (var amenity in amenities)
        {
            var item = CreateAmenityItem(amenity);
            _allAmenities.Add(item);
        }

        HasMoreAmenities = _allAmenities.Count > 8;
        SeeAllAmenitiesText = string.Format(AppResources.GetString("RoomDetail_SeeAllAmenities"), _allAmenities.Count);

        var initialList = _allAmenities.Take(8).ToList();
        foreach (var item in initialList)
        {
            AmenitiesDisplayList.Add(item);
        }
        UpdateAmenityRows();
    }

    private void UpdateAmenityRows()
    {
        AmenityRows.Clear();
        for (int i = 0; i < AmenitiesDisplayList.Count; i += 2)
        {
            var left = AmenitiesDisplayList[i];
            var right = i + 1 < AmenitiesDisplayList.Count ? AmenitiesDisplayList[i + 1] : null;
            AmenityRows.Add(new AmenityRowItem(left, right));
        }
    }

    private static AmenityDisplayItem CreateAmenityItem(Amenity amenity) => amenity switch
    {
        Amenity.AirConditioner => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_AirConditioner"), FluentUI.weather_snowflake_24_regular),
        Amenity.WaterHeater => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_WaterHeater"), FluentUI.drop_24_regular),
        Amenity.PrivateBathroom => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_PrivateBathroom"), FluentUI.drop_24_regular),
        Amenity.Wifi => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Wifi"), FluentUI.wifi_1_24_regular),
        Amenity.Bed => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Bed"), FluentUI.bed_24_regular),
        Amenity.Fridge => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Fridge"), FluentUI.food_24_regular),
        Amenity.MotorbikeParking => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_MotorbikeParking"), FluentUI.vehicle_bicycle_24_regular),
        Amenity.Wardrobe => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Wardrobe"), FluentUI.desk_24_regular),
        Amenity.Desk => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Desk"), FluentUI.desk_24_regular),
        Amenity.Balcony => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Balcony"), FluentUI.window_24_regular),
        Amenity.Window => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Window"), FluentUI.window_24_regular),
        Amenity.WashingMachine => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_WashingMachine"), FluentUI.drop_24_regular),
        Amenity.Kitchen => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Kitchen"), FluentUI.food_24_regular),
        Amenity.Elevator => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_Elevator"), FluentUI.arrow_sort_up_24_regular),
        Amenity.DryingArea => new AmenityDisplayItem(amenity, AppResources.GetString("Amenity_DryingArea"), FluentUI.weather_sunny_high_24_regular),
        _ => new AmenityDisplayItem(amenity, amenity.ToString(), FluentUI.home_24_regular)
    };

    private void PopulateSafety(SafetyDto safety)
    {
        SafetyChecklist.Clear();
        IsSafetyVerified = safety.IsVerified;
        int score = safety.Score <= 0
            ? 0
            : (safety.Score <= 5 ? safety.Score : Math.Clamp((int)Math.Round(safety.Score / 20.0), 0, 5));
        string scoreFormat = safety.IsVerified
            ? AppResources.GetString("RoomDetail_SafetyScoreVerified")
            : AppResources.GetString("RoomDetail_SafetyScoreUnverified");
        SafetyScoreFormatted = string.Format(scoreFormat, score);

        var presentFeatures = safety.Features.ToHashSet();

        // 1. CCTV
        bool hasCctv = presentFeatures.Contains(SafetyFeature.Cctv);
        SafetyChecklist.Add(new SafetyCheckItem(
            SafetyFeature.Cctv,
            AppResources.GetString("Safety_Cctv"),
            hasCctv,
            hasCctv ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));

        // 2. Secure Entrance
        bool hasSecure = presentFeatures.Contains(SafetyFeature.SecureEntrance);
        SafetyChecklist.Add(new SafetyCheckItem(
            SafetyFeature.SecureEntrance,
            AppResources.GetString("Safety_SecureEntrance"),
            hasSecure,
            hasSecure ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));

        // 3. Fire Safety
        bool hasFire = presentFeatures.Contains(SafetyFeature.FireSafety);
        SafetyChecklist.Add(new SafetyCheckItem(
            SafetyFeature.FireSafety,
            AppResources.GetString("Safety_FireExtinguisher"),
            hasFire,
            hasFire ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));

        // 4. Owner / Guard On Site
        bool hasOwner = presentFeatures.Contains(SafetyFeature.OwnerOrGuardOnSite);
        SafetyChecklist.Add(new SafetyCheckItem(
            SafetyFeature.OwnerOrGuardOnSite,
            AppResources.GetString("Safety_OwnerLivesOnSite"),
            hasOwner,
            hasOwner ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));

        // 5. Well-Lit Access
        bool hasLight = presentFeatures.Contains(SafetyFeature.WellLitAccess);
        SafetyChecklist.Add(new SafetyCheckItem(
            SafetyFeature.WellLitAccess,
            AppResources.GetString("Safety_WellLitAccessPath"),
            hasLight,
            hasLight ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));
    }

    private void PopulateHouseRules(HouseRulesDto? rules)
    {
        HouseRulesList.Clear();
        if (rules == null)
        {
            HasHouseRules = false;
            return;
        }

        HasHouseRules = true;

        // Quiet Hours
        if (!string.IsNullOrWhiteSpace(rules.QuietHoursStart) && !string.IsNullOrWhiteSpace(rules.QuietHoursEnd))
        {
            HouseRulesList.Add(new HouseRuleItem(
                FluentUI.weather_moon_20_regular,
                AppResources.GetString("HouseRule_QuietHours"),
                $"{rules.QuietHoursStart} – {rules.QuietHoursEnd}"));
        }

        // Curfew
        if (!string.IsNullOrWhiteSpace(rules.CurfewTime))
        {
            HouseRulesList.Add(new HouseRuleItem(
                FluentUI.clock_20_regular,
                AppResources.GetString("HouseRule_Curfew"),
                string.Format(AppResources.GetString("HouseRule_CurfewGateCloses"), rules.CurfewTime)));
        }

        // Guests
        string guestDetail = rules.GuestPolicy switch
        {
            GuestPolicy.DaytimeOnly => AppResources.GetString("HouseRule_GuestsDaytimeOnly"),
            GuestPolicy.Allowed => AppResources.GetString("HouseRule_GuestsAllowed"),
            _ => AppResources.GetString("HouseRule_GuestsNotAllowed")
        };
        if (!string.IsNullOrWhiteSpace(rules.GuestPolicyNote))
        {
            guestDetail = $"{guestDetail} · {rules.GuestPolicyNote.Trim()}";
        }
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.people_20_regular,
            AppResources.GetString("HouseRule_Guests"),
            guestDetail));

        // Cooking
        string cookingDetail = rules.CookingPolicy switch
        {
            CookingPolicy.SharedKitchenOnly => AppResources.GetString("HouseRule_CookingShared"),
            CookingPolicy.Allowed => AppResources.GetString("HouseRule_CookingInRoom"),
            _ => AppResources.GetString("HouseRule_CookingNotAllowed")
        };
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.food_20_regular,
            AppResources.GetString("HouseRule_Cooking"),
            cookingDetail));

        // Parking (BR-HR-01)
        string parkingDetail;
        if (!rules.ParkingAvailable)
        {
            parkingDetail = AppResources.GetString("HouseRule_ParkingUnavailable");
        }
        else
        {
            parkingDetail = !string.IsNullOrWhiteSpace(rules.ParkingRules)
                ? rules.ParkingRules.Trim()
                : AppResources.GetString("HouseRule_ParkingYard");
        }
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.vehicle_car_20_regular,
            AppResources.GetString("HouseRule_Parking"),
            parkingDetail));

        // Pets
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.animal_cat_20_regular,
            AppResources.GetString("HouseRule_Pets"),
            AppResources.GetString(rules.PetsAllowed ? "HouseRule_PetsAllowed" : "HouseRule_PetsNotAllowed")));

        // Smoking
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.prohibited_20_regular,
            AppResources.GetString("HouseRule_Smoking"),
            AppResources.GetString(rules.SmokingAllowed ? "HouseRule_SmokingAllowed" : "HouseRule_SmokingNotAllowed")));

        // Other Rules
        if (!string.IsNullOrWhiteSpace(rules.OtherRules))
        {
            HouseRulesList.Add(new HouseRuleItem(
                FluentUI.document_endnote_20_regular,
                AppResources.GetString("HouseRule_OtherRules"),
                rules.OtherRules.Trim()));
        }
    }

    private void PopulateLandlord(LandlordContactDto landlord)
    {
        LandlordName = landlord.DisplayName;
        LandlordInitials = ExtractInitials(landlord.DisplayName);
        string since = landlord.MemberSinceUtc.ToString("MMM yyyy", CultureInfo.CurrentCulture);
        LandlordBadge = string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_VerifiedLandlordSince"), since);

        LandlordPhoneNumber = landlord.PhoneNumber;
        LandlordZaloUrl = landlord.ZaloUrl;

        CanCall = !string.IsNullOrWhiteSpace(landlord.PhoneNumber);
        CanZalo = !string.IsNullOrWhiteSpace(landlord.ZaloUrl);
    }

    private static string ExtractInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "LL";
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static string FullMoney(long amountVnd) =>
        CultureFormatting.FormatCurrency(amountVnd, CultureInfo.CurrentCulture);

    private async Task LoadSimilarRoomsAsync()
    {
        try
        {
            SimilarRooms.Clear();
            var similar = await _roomGateway.GetSimilarRoomsAsync(RoomId, CampusId);
            foreach (var card in similar)
            {
                SimilarRooms.Add(new SimilarRoomDisplayModel(
                    card.Id,
                    card.Title,
                    CultureFormatting.FormatMonthlyPrice(card.MonthlyRentVnd, CultureInfo.CurrentCulture),
                    string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_DistanceFromThisRoom"), card.DistanceKm.ToString("0.0", CultureInfo.CurrentCulture)),
                    card.CoverThumbUrl));
            }
            HasSimilarRooms = SimilarRooms.Count > 0;
        }
        catch
        {
            HasSimilarRooms = false;
        }
    }

    private async Task LoadTravelTimesAsync()
    {
        try
        {
            var travel = await _roomGateway.GetRoomTravelTimesAsync(RoomId, CampusId);
            _lastTravelTimes = travel;
            ApplyTravelTimes(travel);
        }
        catch
        {
            _lastTravelTimes = null;
            ApplyTravelTimeFallback();
        }
    }

    private void ApplyTravelTimes(TravelTimesDto? travel)
    {
        if (travel == null)
        {
            ApplyTravelTimeFallback();
            return;
        }

        if (!string.IsNullOrWhiteSpace(travel.CampusId))
        {
            CampusName = travel.CampusId;
        }

        UpdateCommuteNotices(travel.StraightLineDistanceKm);

        CommuteTimes.Clear();
        if (travel.IsFallback || travel.Items.Count == 0)
        {
            HasTravelTimes = false;
            IsCommuteFallback = true;
            return;
        }

        IsCommuteFallback = false;
        HasTravelTimes = true;

        foreach (var item in travel.Items)
        {
            int mins = (int)Math.Max(1, Math.Round(item.DurationSeconds / 60.0));
            string timeFormatted = item.IsEstimate
                ? string.Format(AppResources.GetString("RoomDetail_MinutesEst"), mins)
                : string.Format(AppResources.GetString("RoomDetail_Minutes"), mins);

            var commuteItem = item.Mode switch
            {
                TravelMode.Motorbike => new CommuteModeItem(item.Mode, FluentUI.vehicle_motorcycle_24_regular, timeFormatted, AppResources.GetString("TravelMode_Motorbike")),
                TravelMode.Bus => new CommuteModeItem(item.Mode, FluentUI.vehicle_bus_24_regular, timeFormatted, AppResources.GetString("TravelMode_Bus")),
                _ => new CommuteModeItem(item.Mode, FluentUI.person_walking_24_regular, timeFormatted, AppResources.GetString("TravelMode_Walking"))
            };
            CommuteTimes.Add(commuteItem);
        }
    }

    private void ApplyTravelTimeFallback()
    {
        CommuteTimes.Clear();
        HasTravelTimes = false;
        IsCommuteFallback = true;
        if (Room != null)
        {
            UpdateCommuteNotices(Room.DistanceKm);
        }
    }

    private void UpdateCommuteNotices(double straightLineDistanceKm)
    {
        var distanceFormatted = straightLineDistanceKm.ToString("0.0", CultureInfo.CurrentCulture);
        CommuteHeaderFormatted = string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_GettingToCampus"), CampusName, distanceFormatted);
        StraightLineNotice = string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_StraightLineNotice"), distanceFormatted, CampusName);
    }

    #endregion

    #region Test Overrides

    public Func<string, Task>? NavigateOverride { get; set; }
    public Func<string, string, string, string, Task<bool>>? DisplayAlertOverride { get; set; }
    public Func<string, Task>? ShareOverride { get; set; }
    public Action<string>? PhoneDialerOverride { get; set; }
    public Func<string, Task>? LauncherOverride { get; set; }
    public Func<string, Task>? ToastOverride { get; set; }

    #endregion

    #region Action Commands

    [RelayCommand]
    public async Task GoBackAsync()
    {
        if (NavigateOverride != null)
        {
            await NavigateOverride("..");
            return;
        }

#if !UNIT_TESTS
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
#endif
    }

    [RelayCommand]
    public async Task ShareListingAsync()
    {
        if (Room == null) return;
        // Shared text leaves the app, so it carries the full amount.
        var text = $"{Room.Title} - {FullMoney(Room.MonthlyRentVnd)} ({AddressFormatted})";
        if (ShareOverride != null)
        {
            await ShareOverride(text);
            return;
        }

#if !UNIT_TESTS
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = Room.Title,
            Text = text
        });
#endif
    }

    [RelayCommand]
    public async Task ToggleFavouriteAsync()
    {
        if (Room == null) return;

        IsFavourite = !IsFavourite;
        if (_favouriteGateway != null && IsSignedIn)
        {
            try
            {
                if (IsFavourite)
                    await _favouriteGateway.AddFavouriteAsync(Room.Id);
                else
                    await _favouriteGateway.RemoveFavouriteAsync(Room.Id);
            }
            catch
            {
                // Silently rollback on gateway failure
                IsFavourite = !IsFavourite;
            }
        }
    }

    [RelayCommand]
    public async Task Open360ViewAsync()
    {
        if (ToastOverride != null)
        {
            await ToastOverride(AppResources.GetString("RoomDetail_360View"));
            return;
        }

#if !UNIT_TESTS
        if (Shell.Current != null)
        {
            await AppShell.DisplayToastAsync(AppResources.GetString("RoomDetail_360View"));
        }
#endif
    }

    [RelayCommand]
    public void ToggleAllAmenities()
    {
        IsAllAmenitiesExpanded = !IsAllAmenitiesExpanded;
        AmenitiesDisplayList.Clear();
        var target = IsAllAmenitiesExpanded ? _allAmenities : _allAmenities.Take(8).ToList();
        foreach (var item in target)
        {
            AmenitiesDisplayList.Add(item);
        }
        UpdateAmenityRows();
        SeeAllAmenitiesText = IsAllAmenitiesExpanded
            ? AppResources.GetString("RoomDetail_CollapseAmenities")
            : string.Format(AppResources.GetString("RoomDetail_SeeAllAmenities"), _allAmenities.Count);
    }

    [RelayCommand]
    public async Task CallLandlordAsync()
    {
        if (!IsSignedIn || string.IsNullOrWhiteSpace(LandlordPhoneNumber))
        {
            await PromptSignInAsync();
            return;
        }

        if (PhoneDialerOverride != null)
        {
            PhoneDialerOverride(LandlordPhoneNumber);
            return;
        }

#if !UNIT_TESTS
        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(LandlordPhoneNumber);
            }
            else
            {
                await Launcher.Default.OpenAsync($"tel:{LandlordPhoneNumber}");
            }
        }
        catch
        {
            // Fallback if dialer fails
        }
#endif
    }

    [RelayCommand]
    public async Task OpenZaloAsync()
    {
        if (!IsSignedIn || string.IsNullOrWhiteSpace(LandlordZaloUrl))
        {
            await PromptSignInAsync();
            return;
        }

        if (LauncherOverride != null)
        {
            await LauncherOverride(LandlordZaloUrl);
            return;
        }

#if !UNIT_TESTS
        try
        {
            await Launcher.Default.OpenAsync(LandlordZaloUrl);
        }
        catch
        {
            // Fallback
        }
#endif
    }

    [RelayCommand]
    public async Task MessageLandlordAsync()
    {
        if (!IsSignedIn)
        {
            await PromptSignInAsync();
            return;
        }

        var route = $"//messages?roomId={Room?.Id}&landlordId={Room?.Landlord.Id}";
        if (NavigateOverride != null)
        {
            await NavigateOverride(route);
            return;
        }

#if !UNIT_TESTS
        if (Shell.Current != null && Room != null)
        {
            await Shell.Current.GoToAsync(route);
        }
#endif
    }

    [RelayCommand]
    public async Task ReportListingAsync()
    {
        if (Room == null) return;
        var route = $"report?roomId={Room.Id}";
        if (NavigateOverride != null)
        {
            await NavigateOverride(route);
            return;
        }

#if !UNIT_TESTS
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(route);
        }
#endif
    }

    [RelayCommand]
    public Task PreviewUnavailableDemoAsync() => SelectSimilarRoomAsync("seed-da-thue");

    [RelayCommand]
    public async Task SelectSimilarRoomAsync(string targetRoomId)
    {
        if (string.IsNullOrWhiteSpace(targetRoomId)) return;
        var route = $"{AppRoutes.RoomDetail}?roomId={Uri.EscapeDataString(targetRoomId)}";
        if (!string.IsNullOrWhiteSpace(CampusId))
        {
            route += $"&campusId={Uri.EscapeDataString(CampusId)}";
        }
        if (NavigateOverride != null)
        {
            await NavigateOverride(route);
            return;
        }

#if !UNIT_TESTS
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(route);
        }
#endif
    }

    [RelayCommand]
    public async Task PromptSignInAsync()
    {
        bool proceed = false;
        if (DisplayAlertOverride != null)
        {
            proceed = await DisplayAlertOverride(
                AppResources.GetString("RoomDetail_SignInPromptTitle"),
                AppResources.GetString("RoomDetail_SignInPromptMessage"),
                AppResources.GetString("RoomDetail_SignInAction"),
                AppResources.GetString("Common_Ok"));
        }
#if !UNIT_TESTS
        else if (Shell.Current != null)
        {
            proceed = await Shell.Current.DisplayAlertAsync(
                AppResources.GetString("RoomDetail_SignInPromptTitle"),
                AppResources.GetString("RoomDetail_SignInPromptMessage"),
                AppResources.GetString("RoomDetail_SignInAction"),
                AppResources.GetString("Common_Ok"));
        }
#endif

        if (proceed)
        {
            if (NavigateOverride != null)
            {
                await NavigateOverride("//login");
            }
#if !UNIT_TESTS
            else if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//login");
            }
#endif
        }
    }

    private void OnCultureChanged()
    {
        if (Room != null)
        {
            PopulateRoomDetails(Room);
            if (_lastTravelTimes != null)
            {
                ApplyTravelTimes(_lastTravelTimes);
            }
            else if (IsCommuteFallback)
            {
                ApplyTravelTimeFallback();
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                LocalizedStrings.Instance.PropertyChanged -= _cultureChangedHandler;
            }
            _disposed = true;
        }
    }

    #endregion
}
