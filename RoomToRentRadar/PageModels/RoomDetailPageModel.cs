using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fonts;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Pages.Controls.RoomDetail.Models;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// Presentation PageModel for SCR-ROOM-01 (Room Detail) and SCR-ROOM-01-UNAVAILABLE (Unavailable Listing Handling).
/// Implements AC1-AC4 of US02, BR-LST-03, BR-LST-04a, BR-LST-08, BR-LST-10, BR-AUTH-07, BR-HR-01, BR-CNT-01.
/// </summary>
#if !UNIT_TESTS
[QueryProperty(nameof(RoomId), "roomId")]
[QueryProperty(nameof(CampusId), "campusId")]
#endif
public partial class RoomDetailPageModel : ObservableObject
{
    private readonly IRoomGateway _roomGateway;
    private readonly ISessionStore _sessionStore;
    private readonly IFavouriteGateway? _favouriteGateway;

    private readonly List<AmenityDisplayItem> _allAmenities = [];

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
    public partial bool IsAvailable { get; set; } = true;

    [ObservableProperty]
    public partial bool IsUnavailable { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavouriteIconGlyph))]
    [NotifyPropertyChangedFor(nameof(FavouriteIconColor))]
    public partial bool IsFavourite { get; set; } = false;

    public string FavouriteIconGlyph => FluentUI.heart_24_regular;
    public string FavouriteIconColor => IsFavourite ? "#DC2626" : "#1E293B";

    [ObservableProperty]
    public partial RoomDetailDto? Room { get; set; }

    #endregion

    #region Hero Gallery Properties

    [ObservableProperty]
    public partial int CurrentPhotoIndex { get; set; } = 1;

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

    public ObservableCollection<CostRowItem> CostBreakdownRows { get; } = [];

    [ObservableProperty]
    public partial string EstimatedTotalFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EstimateNoteFormatted { get; set; } = string.Empty;

    #endregion

    #region Amenities Properties

    public ObservableCollection<AmenityDisplayItem> AmenitiesDisplayList { get; } = [];

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

        LocalizedStrings.Instance.PropertyChanged += (_, _) => OnCultureChanged();
    }

    #region Initialization & Data Loading

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomId))
        {
            IsLoading = false;
            IsNotFound = true;
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;
            IsNotFound = false;
            ErrorMessage = string.Empty;

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
            ErrorMessage = !string.IsNullOrWhiteSpace(ex.Message) ? ex.Message : AppResources.GetString("RoomDetail_NotFoundMessage");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    private void PopulateRoomDetails(RoomDetailDto detail)
    {
        Title = detail.Title;
        IsFavourite = detail.IsFavourite;
        Latitude = detail.Location.Latitude;
        Longitude = detail.Location.Longitude;

        // Availability state mapping (BR-LST-04a)
        IsAvailable = detail.Status == ListingStatus.Available;
        IsUnavailable = detail.Status is ListingStatus.Rented or ListingStatus.Unpublished;

        // Pricing formatting
        PriceFormatted = $"{detail.MonthlyRentVnd:N0} đ";
        PeriodFormatted = AppResources.GetString("RoomDetail_PerMonth");
        PriceShortFormatted = $"{FormatShortMoney(detail.MonthlyRentVnd)}/mo";

        int depositMonths = detail.MonthlyRentVnd > 0 ? (int)Math.Round((double)detail.DepositVnd / detail.MonthlyRentVnd) : 1;
        string monthWord = depositMonths <= 1
            ? AppResources.GetString("RoomDetail_DepositMonth")
            : AppResources.GetString("RoomDetail_DepositMonths");
        string depositPeriod = string.Format(monthWord, depositMonths);
        DepositFormatted = string.Format(AppResources.GetString("RoomDetail_Deposit"), $"{detail.DepositVnd:N0} đ", depositPeriod);
        DepositShortFormatted = string.Format(AppResources.GetString("RoomDetail_DepositShort"), FormatShortMoney(detail.DepositVnd));

        // Status badge & relative updated text
        StatusBadgeText = detail.IsVerified && IsAvailable
            ? AppResources.GetString("RoomDetail_VerifiedAvailable")
            : (IsAvailable ? AppResources.GetString("RoomDetail_StatusAvailable") : AppResources.GetString("RoomDetail_StatusRented"));

        int daysAgo = Math.Max(0, (DateTime.UtcNow.Date - detail.LastUpdatedAtUtc.Date).Days);
        UpdatedAgoText = daysAgo == 0
            ? AppResources.GetString("RoomDetail_UpdatedToday")
            : (daysAgo == 1
                ? AppResources.GetString("RoomDetail_UpdatedYesterday")
                : string.Format(AppResources.GetString("RoomDetail_UpdatedDaysAgo"), daysAgo));

        // Unavailable banner text (Image 4)
        UnavailableBannerText = string.Format(AppResources.GetString("RoomDetail_UnavailableBanner"), daysAgo == 0 ? AppResources.GetString("RoomDetail_UpdatedToday").ToLowerInvariant() : $"{daysAgo} days ago");
        ContactDisabledText = AppResources.GetString("RoomDetail_ContactDisabled");

        // Photos gallery setup
        Photos.Clear();
        foreach (var photo in detail.Photos)
        {
            Photos.Add(photo);
        }
        TotalPhotosCount = Math.Max(1, detail.Photos.Count);
        CurrentPhotoIndex = 1;
        PhotoCounterText = $"{CurrentPhotoIndex} / {TotalPhotosCount}";
        HasPhotos = detail.Photos.Count > 0;
        HeroCoverUrl = detail.Photos.OrderBy(p => p.SortOrder).FirstOrDefault()?.MediumUrl
            ?? detail.Photos.FirstOrDefault()?.MediumUrl;
        Is360Available = detail.Photos.Any(p => p.Kind == PhotoKind.Panorama);

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
        CampusName = !string.IsNullOrWhiteSpace(CampusId) ? CampusId : "ICTU";
        CommuteHeaderFormatted = string.Format(AppResources.GetString("RoomDetail_GettingToCampus"), CampusName, detail.DistanceKm.ToString("0.0", CultureInfo.InvariantCulture));
        StraightLineNotice = string.Format(AppResources.GetString("RoomDetail_StraightLineNotice"), detail.DistanceKm.ToString("0.0", CultureInfo.InvariantCulture), CampusName);

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
            $"{detail.AreaM2:0.#} m²"));

        // 2. Room Type
        string roomTypeString = detail.RoomType switch
        {
            RoomType.BoardingRoom => AppResources.GetString("RoomType_BoardingRoom"),
            RoomType.Dormitory => AppResources.GetString("RoomType_Dormitory"),
            _ => detail.RoomType.ToString()
        };
        KeyFacts.Add(new KeyFactItem(
            FluentUI.home_24_regular,
            AppResources.GetString("RoomDetail_RoomType"),
            roomTypeString));

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
            ? detail.AvailableFrom.Value.ToString("d MMM", CultureInfo.InvariantCulture)
            : AppResources.GetString("RoomDetail_StatusAvailable");
        KeyFacts.Add(new KeyFactItem(
            FluentUI.calendar_ltr_24_regular,
            AppResources.GetString("RoomDetail_AvailableFrom"),
            availableFromText));

        // 6. Distance to Campus
        KeyFacts.Add(new KeyFactItem(
            FluentUI.location_24_regular,
            string.Format(AppResources.GetString("RoomDetail_ToCampus"), CampusName),
            $"{detail.DistanceKm:0.0} km"));
    }

    private void PopulateMonthlyCosts(RoomDetailDto detail)
    {
        CostBreakdownRows.Clear();
        var utils = detail.Utilities;

        // Rent
        CostBreakdownRows.Add(new CostRowItem(
            AppResources.GetString("RoomDetail_Rent"),
            $"{detail.MonthlyRentVnd:N0} đ"));

        // Electricity
        string elecValue = utils.ElectricityPriceVndPerKwh.HasValue
            ? string.Format(AppResources.GetString("RoomDetail_PerKwh"), $"{utils.ElectricityPriceVndPerKwh.Value:N0}")
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
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerPerson"), $"{utils.WaterPriceVnd.Value:N0}");
        }
        else
        {
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerM3"), $"{utils.WaterPriceVnd.Value:N0}");
        }
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_Water"), waterValue));

        // Internet
        string internetValue = utils.InternetFeeVnd.HasValue && utils.InternetFeeVnd.Value > 0
            ? $"{utils.InternetFeeVnd.Value:N0} đ"
            : AppResources.GetString("RoomDetail_Included");
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_Internet"), internetValue));

        // Motorbike Parking
        string parkingValue = utils.ParkingFeeVnd.HasValue && utils.ParkingFeeVnd.Value > 0
            ? $"{utils.ParkingFeeVnd.Value:N0} đ"
            : AppResources.GetString("RoomDetail_Free");
        CostBreakdownRows.Add(new CostRowItem(AppResources.GetString("RoomDetail_MotorbikeParking"), parkingValue));

        // Total
        EstimatedTotalFormatted = string.Format(AppResources.GetString("RoomDetail_EstimatedMonthlyFormat"), FormatShortMoney(utils.EstimatedMonthlyTotalVnd));
        EstimateNoteFormatted = AppResources.GetString("RoomDetail_EstimateNote");
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
        _ => new AmenityDisplayItem(amenity, amenity.ToString(), FluentUI.home_24_regular)
    };

    private void PopulateSafety(SafetyDto safety)
    {
        SafetyChecklist.Clear();
        int score = safety.Score > 0 ? (safety.Score <= 5 ? safety.Score : (int)Math.Round(safety.Score / 20.0)) : 4;
        SafetyScoreFormatted = string.Format(AppResources.GetString("RoomDetail_SafetyScoreVerified"), score);

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

        // Parking
        string parkingDetail = !string.IsNullOrWhiteSpace(rules.ParkingRules)
            ? rules.ParkingRules
            : AppResources.GetString("HouseRule_ParkingYard");
        HouseRulesList.Add(new HouseRuleItem(
            FluentUI.vehicle_car_20_regular,
            AppResources.GetString("HouseRule_Parking"),
            parkingDetail));
    }

    private void PopulateLandlord(LandlordContactDto landlord)
    {
        LandlordName = landlord.DisplayName;
        LandlordInitials = ExtractInitials(landlord.DisplayName);
        string since = landlord.MemberSinceUtc.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        LandlordBadge = string.Format(AppResources.GetString("RoomDetail_VerifiedLandlordSince"), since);

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

    private static string FormatShortMoney(long amountVnd)
    {
        if (amountVnd >= 1_000_000)
        {
            double m = amountVnd / 1_000_000.0;
            return m.ToString("0.##", CultureInfo.InvariantCulture) + "M";
        }
        return $"{amountVnd:N0}";
    }

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
                    $"{FormatShortMoney(card.MonthlyRentVnd)}/mo",
                    string.Format(AppResources.GetString("RoomDetail_DistanceFromThisRoom"), card.DistanceKm.ToString("0.0", CultureInfo.InvariantCulture)),
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
            CommuteTimes.Clear();
            var travel = await _roomGateway.GetRoomTravelTimesAsync(RoomId, CampusId);
            if (travel != null && travel.Items.Count > 0)
            {
                HasTravelTimes = true;
                IsCommuteFallback = travel.IsFallback;

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
            else
            {
                PopulateDefaultEstimatedCommute();
            }
        }
        catch
        {
            PopulateDefaultEstimatedCommute();
        }
    }

    private void PopulateDefaultEstimatedCommute()
    {
        CommuteTimes.Clear();
        if (Room == null) return;

        double distanceKm = Room.DistanceKm;
        int motorbikeMins = (int)Math.Max(1, Math.Round(distanceKm / 30.0 * 60));
        int busMins = (int)Math.Max(5, Math.Round(distanceKm / 15.0 * 60 + 5));
        int walkMins = (int)Math.Max(2, Math.Round(distanceKm / 5.0 * 60));

        CommuteTimes.Add(new CommuteModeItem(TravelMode.Motorbike, FluentUI.vehicle_motorcycle_24_regular, string.Format(AppResources.GetString("RoomDetail_Minutes"), motorbikeMins), AppResources.GetString("TravelMode_Motorbike")));
        CommuteTimes.Add(new CommuteModeItem(TravelMode.Bus, FluentUI.vehicle_bus_24_regular, string.Format(AppResources.GetString("RoomDetail_MinutesEst"), busMins), AppResources.GetString("TravelMode_Bus")));
        CommuteTimes.Add(new CommuteModeItem(TravelMode.Walking, FluentUI.person_walking_24_regular, string.Format(AppResources.GetString("RoomDetail_Minutes"), walkMins), AppResources.GetString("TravelMode_Walking")));
        HasTravelTimes = true;
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
        var text = $"{Room.Title} - {PriceFormatted} ({AddressFormatted})";
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
    public async Task SelectSimilarRoomAsync(string targetRoomId)
    {
        if (string.IsNullOrWhiteSpace(targetRoomId)) return;
        var route = $"roomdetail?roomId={targetRoomId}&campusId={CampusId}";
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

    private async Task PromptSignInAsync()
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
        }
    }

    #endregion
}
