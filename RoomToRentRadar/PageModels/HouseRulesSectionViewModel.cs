using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// Item hiển thị một phiên bản nội quy đã lưu trữ trong lịch sử (US11 / T11.2).
/// Pure C# để chạy trực tiếp trong RoomToRentRadarUnitTests (net10.0).
/// </summary>
public sealed class HouseRulesHistoryItemModel
{
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;

    public string VersionBadgeText => $"v{Version}";
    public string FormattedUpdatedAt => UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

/// <summary>
/// ViewModel cho HouseRulesSectionView có hỗ trợ chỉnh sửa và quản lý phiên bản nội quy (US11 / T11.2).
/// </summary>
public partial class HouseRulesSectionViewModel : ObservableObject
{
    private readonly IHouseRulesService _houseRulesService;

    private string _listingId = "room-2026-001";
    private int _version = 2;
    private DateTimeOffset _updatedAt = new(2026, 11, 23, 8, 0, 0, TimeSpan.Zero);
    private string _quietHours = "23:00 - 05:30";
    private string _guestPolicy = "Khách về trước 22:30, qua đêm báo trước chủ nhà";
    private string _cookingRules = "Được nấu ăn bằng bếp từ, không dùng bếp gas mini";
    private string _parkingRules = "Để xe đúng vạch tầng 1, khóa cổ xe";

    private bool _isEditing;
    private string _draftQuietHours = string.Empty;
    private string _draftGuestPolicy = string.Empty;
    private string _draftCookingRules = string.Empty;
    private string _draftParkingRules = string.Empty;

    private bool _isHistoryExpanded;
    private bool _isSaving;
    private string _validationError = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isFromLiveApi;

    public HouseRulesSectionViewModel(IHouseRulesService? houseRulesService = null)
    {
        _houseRulesService = houseRulesService ?? new MockHouseRulesService();

        ToggleEditModeCommand = new RelayCommand(ToggleEditMode);
        CancelEditCommand = new RelayCommand(CancelEdit);
        SaveRulesCommand = new AsyncRelayCommand(SaveRulesAsync);
        ToggleHistoryCommand = new RelayCommand(ToggleHistory);
        RestoreVersionCommand = new AsyncRelayCommand<HouseRulesHistoryItemModel>(RestoreVersionAsync);

        SeedInitialHistory();
    }

    public ObservableCollection<HouseRulesHistoryItemModel> VersionHistory { get; } = [];

    public IRelayCommand ToggleEditModeCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand SaveRulesCommand { get; }
    public IRelayCommand ToggleHistoryCommand { get; }
    public IAsyncRelayCommand<HouseRulesHistoryItemModel> RestoreVersionCommand { get; }

    public string ListingId
    {
        get => _listingId;
        set => SetProperty(ref _listingId, value);
    }

    public int Version
    {
        get => _version;
        private set
        {
            if (SetProperty(ref _version, value))
            {
                OnPropertyChanged(nameof(VersionBadgeText));
            }
        }
    }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        private set
        {
            if (SetProperty(ref _updatedAt, value))
            {
                OnPropertyChanged(nameof(VersionBadgeText));
            }
        }
    }

    public string QuietHours
    {
        get => _quietHours;
        private set => SetProperty(ref _quietHours, value);
    }

    public string GuestPolicy
    {
        get => _guestPolicy;
        private set => SetProperty(ref _guestPolicy, value);
    }

    public string CookingRules
    {
        get => _cookingRules;
        private set => SetProperty(ref _cookingRules, value);
    }

    public string ParkingRules
    {
        get => _parkingRules;
        private set => SetProperty(ref _parkingRules, value);
    }

    public string VersionBadgeText => $"Phiên bản v{Version} • Cập nhật {UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}";

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsNotEditing));
                OnPropertyChanged(nameof(EditButtonText));
            }
        }
    }

    public bool IsNotEditing => !IsEditing;

    public string EditButtonText => IsEditing ? "Đóng chỉnh sửa" : "✏️ Sửa nội quy";

    public string DraftQuietHours
    {
        get => _draftQuietHours;
        set
        {
            if (SetProperty(ref _draftQuietHours, value))
            {
                ValidationError = string.Empty;
            }
        }
    }

    public string DraftGuestPolicy
    {
        get => _draftGuestPolicy;
        set
        {
            if (SetProperty(ref _draftGuestPolicy, value))
            {
                ValidationError = string.Empty;
            }
        }
    }

    public string DraftCookingRules
    {
        get => _draftCookingRules;
        set
        {
            if (SetProperty(ref _draftCookingRules, value))
            {
                ValidationError = string.Empty;
            }
        }
    }

    public string DraftParkingRules
    {
        get => _draftParkingRules;
        set
        {
            if (SetProperty(ref _draftParkingRules, value))
            {
                ValidationError = string.Empty;
            }
        }
    }

    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        private set
        {
            if (SetProperty(ref _isHistoryExpanded, value))
            {
                OnPropertyChanged(nameof(HistoryToggleText));
            }
        }
    }

    public bool HasHistory => VersionHistory.Count > 0;

    public string HistoryToggleText => IsHistoryExpanded
        ? $"▲ Ẩn lịch sử phiên bản ({VersionHistory.Count})"
        : $"▼ Xem lịch sử phiên bản ({VersionHistory.Count})";

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    public string ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public bool IsFromLiveApi
    {
        get => _isFromLiveApi;
        private set
        {
            if (SetProperty(ref _isFromLiveApi, value))
            {
                OnPropertyChanged(nameof(DataSourceBadgeText));
            }
        }
    }

    public string DataSourceBadgeText => IsFromLiveApi ? "🟢 API T11.1 trực tuyến" : "⚡ Phiên bản Offline";

    private void ToggleEditMode()
    {
        if (!IsEditing)
        {
            DraftQuietHours = QuietHours;
            DraftGuestPolicy = GuestPolicy;
            DraftCookingRules = CookingRules;
            DraftParkingRules = ParkingRules;
            ValidationError = string.Empty;
            IsEditing = true;
        }
        else
        {
            CancelEdit();
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        ValidationError = string.Empty;
        DraftQuietHours = QuietHours;
        DraftGuestPolicy = GuestPolicy;
        DraftCookingRules = CookingRules;
        DraftParkingRules = ParkingRules;
    }

    private async Task SaveRulesAsync()
    {
        if (IsSaving)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DraftQuietHours)
            || string.IsNullOrWhiteSpace(DraftGuestPolicy)
            || string.IsNullOrWhiteSpace(DraftCookingRules)
            || string.IsNullOrWhiteSpace(DraftParkingRules))
        {
            ValidationError = "Vui lòng nhập đầy đủ cả 4 mục nội quy trước khi lưu phiên bản mới.";
            return;
        }

        var currentSnapshot = new HouseRulesResponseContract
        {
            ListingId = ListingId,
            Version = Version,
            UpdatedAt = UpdatedAt,
            QuietHours = QuietHours,
            GuestPolicy = GuestPolicy,
            CookingRules = CookingRules,
            ParkingRules = ParkingRules,
            History = VersionHistory.Select(h => new HouseRulesVersionSnapshotContract
            {
                Version = h.Version,
                UpdatedAt = h.UpdatedAt,
                QuietHours = h.QuietHours,
                GuestPolicy = h.GuestPolicy,
                CookingRules = h.CookingRules,
                ParkingRules = h.ParkingRules
            }).ToList()
        };

        var request = new UpdateHouseRulesRequestContract
        {
            QuietHours = DraftQuietHours.Trim(),
            GuestPolicy = DraftGuestPolicy.Trim(),
            CookingRules = DraftCookingRules.Trim(),
            ParkingRules = DraftParkingRules.Trim()
        };

        try
        {
            IsSaving = true;
            ValidationError = string.Empty;

            var result = await _houseRulesService.UpdateHouseRulesAsync(ListingId, request, currentSnapshot);
            IsFromLiveApi = result.IsFromLiveApi;

            var updated = result.Rules;
            Version = updated.Version > 0 ? updated.Version : Version + 1;
            UpdatedAt = updated.UpdatedAt;
            QuietHours = updated.QuietHours;
            GuestPolicy = updated.GuestPolicy;
            CookingRules = updated.CookingRules;
            ParkingRules = updated.ParkingRules;

            VersionHistory.Clear();
            foreach (var snapshot in updated.History.OrderByDescending(h => h.Version))
            {
                VersionHistory.Add(new HouseRulesHistoryItemModel
                {
                    Version = snapshot.Version,
                    UpdatedAt = snapshot.UpdatedAt,
                    QuietHours = snapshot.QuietHours,
                    GuestPolicy = snapshot.GuestPolicy,
                    CookingRules = snapshot.CookingRules,
                    ParkingRules = snapshot.ParkingRules
                });
            }

            IsEditing = false;
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(HistoryToggleText));
            StatusMessage = $"Đã lưu nội quy phiên bản v{Version} và lưu trữ bản cũ vào lịch sử.";
        }
        catch (Exception ex)
        {
            ValidationError = $"Không thể cập nhật nội quy: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ToggleHistory()
    {
        IsHistoryExpanded = !IsHistoryExpanded;
    }

    private async Task RestoreVersionAsync(HouseRulesHistoryItemModel? historicalVersion)
    {
        if (historicalVersion is null)
        {
            return;
        }

        DraftQuietHours = historicalVersion.QuietHours;
        DraftGuestPolicy = historicalVersion.GuestPolicy;
        DraftCookingRules = historicalVersion.CookingRules;
        DraftParkingRules = historicalVersion.ParkingRules;
        await SaveRulesAsync();
    }

    private void SeedInitialHistory()
    {
        VersionHistory.Add(new HouseRulesHistoryItemModel
        {
            Version = 1,
            UpdatedAt = new DateTimeOffset(2026, 11, 15, 9, 30, 0, TimeSpan.Zero),
            QuietHours = "22:30 - 06:00",
            GuestPolicy = "Khách về trước 22:00, không tụ tập ồn ào",
            CookingRules = "Chỉ đun nước ấm siêu tốc, hạn chế chiên xào",
            ParkingRules = "Để xe ở sân trước cổng, khóa càng cẩn thận"
        });

        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HistoryToggleText));
    }
}
