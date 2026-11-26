using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fonts;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RoomDetailPageModel>? _logger;
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
        IFavouriteGateway? favouriteGateway = null,
        ILogger<RoomDetailPageModel>? logger = null)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _favouriteGateway = favouriteGateway;
        _logger = logger;

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
        UnavailableSummaryFormatted = $"{PriceShortFormatted} · {CultureFormatting.FormatArea(detail.AreaM2, culture)} · {RoomDetailSectionMapper.FormatRoomType(detail.RoomType)}";

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
        foreach (var item in RoomDetailSectionMapper.BuildKeyFacts(detail, CampusName, CultureInfo.CurrentCulture))
        {
            KeyFacts.Add(item);
        }

        UpdateKeyFactRows();
    }

    private void UpdateKeyFactRows()
    {
        KeyFactRows.Clear();
        foreach (var row in RoomDetailSectionMapper.GroupKeyFactRows(KeyFacts))
        {
            KeyFactRows.Add(row);
        }
    }

    private void PopulateMonthlyCosts(RoomDetailDto detail)
    {
        CostBreakdownRows.Clear();
        var culture = CultureInfo.CurrentCulture;
        foreach (var row in RoomDetailSectionMapper.BuildCostBreakdownRows(detail, culture))
        {
            CostBreakdownRows.Add(row);
        }

        // Total & Other Fees Note (BR-LST-03)
        var utils = detail.Utilities;
        EstimatedTotalFormatted = string.Format(culture, AppResources.GetString("RoomDetail_EstimatedMonthlyFormat"), CultureFormatting.FormatCompactMoney(utils.EstimatedMonthlyTotalVnd, culture));
        EstimateNoteFormatted = AppResources.GetString("RoomDetail_EstimateNote");
        HasOtherFeesNote = !string.IsNullOrWhiteSpace(utils.OtherFeesNote);
        OtherFeesNoteFormatted = HasOtherFeesNote
            ? string.Format(culture, AppResources.GetString("RoomDetail_OtherFeesFormat"), utils.OtherFeesNote!.Trim())
            : string.Empty;
    }

    private void PopulateAmenities(IReadOnlyList<Amenity> amenities)
    {
        _allAmenities.Clear();
        AmenitiesDisplayList.Clear();

        _allAmenities.AddRange(RoomDetailSectionMapper.BuildAmenities(amenities));

        HasMoreAmenities = _allAmenities.Count > 8;
        SeeAllAmenitiesText = string.Format(AppResources.GetString("RoomDetail_SeeAllAmenities"), _allAmenities.Count);

        foreach (var item in _allAmenities.Take(8))
        {
            AmenitiesDisplayList.Add(item);
        }

        UpdateAmenityRows();
    }

    private void UpdateAmenityRows()
    {
        AmenityRows.Clear();
        foreach (var row in RoomDetailSectionMapper.GroupAmenityRows(AmenitiesDisplayList))
        {
            AmenityRows.Add(row);
        }
    }

    private void PopulateSafety(SafetyDto safety)
    {
        SafetyChecklist.Clear();
        IsSafetyVerified = safety.IsVerified;
        var (scoreFormatted, items) = RoomDetailSectionMapper.BuildSafetyChecklist(safety);
        SafetyScoreFormatted = scoreFormatted;
        foreach (var item in items)
        {
            SafetyChecklist.Add(item);
        }
    }

    private void PopulateHouseRules(HouseRulesDto? rules)
    {
        HouseRulesList.Clear();
        var items = RoomDetailSectionMapper.BuildHouseRules(rules);
        HasHouseRules = items.Count > 0;
        foreach (var item in items)
        {
            HouseRulesList.Add(item);
        }
    }

    private void PopulateLandlord(LandlordContactDto landlord)
    {
        LandlordName = landlord.DisplayName;
        LandlordInitials = RoomDetailSectionMapper.ExtractInitials(landlord.DisplayName);
        string since = landlord.MemberSinceUtc.ToString("MMM yyyy", CultureInfo.CurrentCulture);
        LandlordBadge = string.Format(CultureInfo.CurrentCulture, AppResources.GetString("RoomDetail_VerifiedLandlordSince"), since);

        LandlordPhoneNumber = landlord.PhoneNumber;
        LandlordZaloUrl = landlord.ZaloUrl;

        CanCall = !string.IsNullOrWhiteSpace(landlord.PhoneNumber);
        CanZalo = !string.IsNullOrWhiteSpace(landlord.ZaloUrl);
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load similar rooms for room {RoomId}", RoomId);
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load travel times for room {RoomId}", RoomId);
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

        foreach (var commuteItem in RoomDetailSectionMapper.BuildCommuteItems(travel.Items))
        {
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
            catch (OperationCanceledException)
            {
                IsFavourite = !IsFavourite;
                throw;
            }
            catch (Exception ex)
            {
                // Silently rollback on gateway failure
                _logger?.LogWarning(ex, "Failed to toggle favourite for room {RoomId}", Room.Id);
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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open phone dialer for room {RoomId}", Room?.Id);
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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open Zalo launcher for room {RoomId}", Room?.Id);
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
