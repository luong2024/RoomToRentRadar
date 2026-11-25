using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// ViewModel cho màn hình so sánh tối đa 3 phòng trọ cạnh nhau (US10 / T10.1).
/// </summary>
public partial class CompareRoomsViewModel : ObservableObject
{
    public const int MaxCompareRooms = 3;

    private const string NormalBgHex = "#F8FAFC";
    private const string NormalTextHex = "#0F172A";
    private const string WinnerBgHex = "#DCFCE7";
    private const string WinnerTextHex = "#15803D";

    private readonly IListingsService _listingsService;

    private bool _isPickerOpen;
    private bool _isBusy;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _statusMessage = "Chọn tối đa 3 phòng quanh ICTU để so sánh cạnh nhau.";
    private string _bestValueSummaryText = string.Empty;

    public CompareRoomsViewModel(IListingsService? listingsService = null)
    {
        _listingsService = listingsService ?? new MockListingsService();

        LoadAvailableRoomsCommand = new AsyncRelayCommand(LoadAvailableRoomsAsync);
        ToggleRoomPickerCommand = new RelayCommand(ToggleRoomPicker);
        AddRoomCommand = new RelayCommand<RoomComparisonItemModel>(AddRoom);
        RemoveRoomCommand = new RelayCommand<RoomComparisonItemModel>(RemoveRoom);
        ClearComparisonCommand = new RelayCommand(ClearComparison);
        ShareComparisonImageCommand = new AsyncRelayCommand(ShareComparisonImageAsync);
    }

    public event Func<Task>? ShareComparisonImageRequested;

    public ObservableCollection<RoomComparisonItemModel> ComparedRooms { get; } = [];
    public ObservableCollection<RoomComparisonItemModel> AvailableRooms { get; } = [];
    public ObservableCollection<ComparisonAttributeRowModel> ComparisonRows { get; } = [];

    public IAsyncRelayCommand LoadAvailableRoomsCommand { get; }
    public IRelayCommand ToggleRoomPickerCommand { get; }
    public IRelayCommand<RoomComparisonItemModel> AddRoomCommand { get; }
    public IRelayCommand<RoomComparisonItemModel> RemoveRoomCommand { get; }
    public IRelayCommand ClearComparisonCommand { get; }
    public IAsyncRelayCommand ShareComparisonImageCommand { get; }

    public RoomComparisonItemModel? Slot1 => ComparedRooms.Count >= 1 ? ComparedRooms[0] : null;
    public RoomComparisonItemModel? Slot2 => ComparedRooms.Count >= 2 ? ComparedRooms[1] : null;
    public RoomComparisonItemModel? Slot3 => ComparedRooms.Count >= 3 ? ComparedRooms[2] : null;

    public bool HasSlot1 => Slot1 is not null;
    public bool HasSlot2 => Slot2 is not null;
    public bool HasSlot3 => Slot3 is not null;

    public bool HasComparedRooms => ComparedRooms.Count > 0;
    public bool HasNoComparedRooms => ComparedRooms.Count == 0;
    public bool CanAddMoreRooms => ComparedRooms.Count < MaxCompareRooms;
    public bool HasBestValueSummary => ComparedRooms.Count >= 2 && !string.IsNullOrEmpty(BestValueSummaryText);

    public string CompareCountText => $"Đang so sánh {ComparedRooms.Count}/{MaxCompareRooms} phòng";

    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set => SetProperty(ref _isPickerOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BestValueSummaryText
    {
        get => _bestValueSummaryText;
        private set
        {
            if (SetProperty(ref _bestValueSummaryText, value))
            {
                OnPropertyChanged(nameof(HasBestValueSummary));
            }
        }
    }

    public async Task LoadAvailableRoomsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var page = await _listingsService.GetListingsAsync(
                campusId: "ictu-main",
                pageIndex: 1,
                pageSize: 12,
                sortBy: "distance");

            AvailableRooms.Clear();
            var index = 0;
            foreach (var item in page.Items)
            {
                var mapped = RoomComparisonItemModel.FromListingItem(item, index++);
                mapped.IsInComparison = ComparedRooms.Any(c => string.Equals(c.Id, mapped.Id, StringComparison.OrdinalIgnoreCase));
                AvailableRooms.Add(mapped);
            }

            if (ComparedRooms.Count == 0 && AvailableRooms.Count >= 2)
            {
                AddRoomInternal(AvailableRooms[0]);
                AddRoomInternal(AvailableRooms[1]);
                RebuildComparisonMatrix();
            }
            else if (ComparedRooms.Count == 0 && AvailableRooms.Count == 1)
            {
                AddRoomInternal(AvailableRooms[0]);
                RebuildComparisonMatrix();
            }
            else
            {
                NotifySlotAndSummaryProperties();
            }

            StatusMessage = AvailableRooms.Count > 0
                ? $"Đã tải {AvailableRooms.Count} phòng quanh ICTU để đối chiếu."
                : "Chưa có phòng nào khả dụng để so sánh.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Không thể tải danh sách phòng để so sánh: {ex.Message}";
            StatusMessage = "Lỗi tải dữ liệu phòng so sánh.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleRoomPicker()
    {
        IsPickerOpen = !IsPickerOpen;
    }

    private void AddRoom(RoomComparisonItemModel? room)
    {
        if (room is null)
        {
            return;
        }

        if (ComparedRooms.Any(r => string.Equals(r.Id, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Phòng này đã có trong bảng so sánh.";
            return;
        }

        if (ComparedRooms.Count >= MaxCompareRooms)
        {
            StatusMessage = "Chỉ được so sánh tối đa 3 phòng cùng lúc. Hãy xóa bớt 1 phòng trước khi thêm.";
            return;
        }

        AddRoomInternal(room);
        RebuildComparisonMatrix();
        StatusMessage = $"Đã thêm \"{room.ShortTitle}\" ({ComparedRooms.Count}/{MaxCompareRooms} phòng).";

        if (ComparedRooms.Count >= MaxCompareRooms)
        {
            IsPickerOpen = false;
        }
    }

    private void AddRoomInternal(RoomComparisonItemModel room)
    {
        ComparedRooms.Add(room);
        room.IsInComparison = true;

        var inAvailable = AvailableRooms.FirstOrDefault(a => string.Equals(a.Id, room.Id, StringComparison.OrdinalIgnoreCase));
        if (inAvailable is not null)
        {
            inAvailable.IsInComparison = true;
        }
    }

    private void RemoveRoom(RoomComparisonItemModel? room)
    {
        if (room is null)
        {
            return;
        }

        var existing = ComparedRooms.FirstOrDefault(r => string.Equals(r.Id, room.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        ComparedRooms.Remove(existing);
        existing.IsInComparison = false;
        existing.IsBestRent = false;
        existing.IsLargestArea = false;
        existing.IsClosestToIctu = false;
        existing.IsHighestSecurity = false;

        var inAvailable = AvailableRooms.FirstOrDefault(a => string.Equals(a.Id, room.Id, StringComparison.OrdinalIgnoreCase));
        if (inAvailable is not null)
        {
            inAvailable.IsInComparison = false;
        }

        RebuildComparisonMatrix();
        StatusMessage = ComparedRooms.Count > 0
            ? $"Đã bỏ \"{existing.ShortTitle}\" khỏi bảng so sánh."
            : "Đã xóa hết phòng. Chọn phòng mới để so sánh.";
    }

    private void ClearComparison()
    {
        foreach (var room in ComparedRooms)
        {
            room.IsInComparison = false;
            room.IsBestRent = false;
            room.IsLargestArea = false;
            room.IsClosestToIctu = false;
            room.IsHighestSecurity = false;
        }

        foreach (var available in AvailableRooms)
        {
            available.IsInComparison = false;
        }

        ComparedRooms.Clear();
        RebuildComparisonMatrix();
        StatusMessage = "Đã xóa danh sách so sánh. Bấm \"Thêm phòng so sánh\" để chọn lại.";
    }

    private async Task ShareComparisonImageAsync()
    {
        if (ComparedRooms.Count < 2)
        {
            StatusMessage = "Cần chọn ít nhất 2 phòng để xuất ảnh so sánh.";
            return;
        }

        if (ShareComparisonImageRequested is not null)
        {
            await ShareComparisonImageRequested.Invoke();
        }

        StatusMessage = $"Sẵn sàng chia sẻ bảng so sánh {ComparedRooms.Count} phòng.";
    }

    private void RebuildComparisonMatrix()
    {
        ComparisonRows.Clear();

        if (ComparedRooms.Count == 0)
        {
            BestValueSummaryText = string.Empty;
            NotifySlotAndSummaryProperties();
            return;
        }

        bool evaluateHighlights = ComparedRooms.Count >= 2;
        decimal minRent = evaluateHighlights ? ComparedRooms.Min(r => r.Rent) : -1m;
        double maxArea = evaluateHighlights ? ComparedRooms.Max(r => r.Area) : -1d;
        double minDistance = evaluateHighlights ? ComparedRooms.Min(r => r.DistanceMeters) : -1d;
        double maxSecurity = evaluateHighlights ? ComparedRooms.Max(r => r.SecurityScore) : -1d;

        foreach (var room in ComparedRooms)
        {
            room.IsBestRent = evaluateHighlights && room.Rent == minRent;
            room.IsLargestArea = evaluateHighlights && Math.Abs(room.Area - maxArea) < 0.01;
            room.IsClosestToIctu = evaluateHighlights && Math.Abs(room.DistanceMeters - minDistance) < 0.01;
            room.IsHighestSecurity = evaluateHighlights && Math.Abs(room.SecurityScore - maxSecurity) < 0.01;
        }

        var r1 = Slot1!;
        var r2 = Slot2;
        var r3 = Slot3;

        ComparisonRows.Add(CreateRow("rent", "💰", "Giá thuê/tháng",
            r1.FormattedRent, r1.IsBestRent,
            r2?.FormattedRent, r2?.IsBestRent ?? false,
            r3?.FormattedRent, r3?.IsBestRent ?? false));

        ComparisonRows.Add(CreateRow("deposit", "🏦", "Tiền cọc",
            r1.FormattedDeposit, false,
            r2?.FormattedDeposit, false,
            r3?.FormattedDeposit, false));

        ComparisonRows.Add(CreateRow("area", "📐", "Diện tích",
            r1.FormattedArea, r1.IsLargestArea,
            r2?.FormattedArea, r2?.IsLargestArea ?? false,
            r3?.FormattedArea, r3?.IsLargestArea ?? false));

        var minUnitPrice = evaluateHighlights ? ComparedRooms.Min(r => r.PricePerSqm) : -1m;
        ComparisonRows.Add(CreateRow("unitPrice", "📊", "Đơn giá / m²",
            r1.FormattedPricePerSqm, evaluateHighlights && r1.PricePerSqm == minUnitPrice,
            r2?.FormattedPricePerSqm, r2 is not null && evaluateHighlights && r2.PricePerSqm == minUnitPrice,
            r3?.FormattedPricePerSqm, r3 is not null && evaluateHighlights && r3.PricePerSqm == minUnitPrice));

        ComparisonRows.Add(CreateRow("distance", "🛵", "Cách ICTU",
            r1.FormattedDistance, r1.IsClosestToIctu,
            r2?.FormattedDistance, r2?.IsClosestToIctu ?? false,
            r3?.FormattedDistance, r3?.IsClosestToIctu ?? false));

        ComparisonRows.Add(CreateRow("roomType", "🏠", "Loại phòng",
            r1.RoomType, false,
            r2?.RoomType, false,
            r3?.RoomType, false));

        ComparisonRows.Add(CreateRow("security", "🛡️", "Điểm an ninh",
            r1.FormattedSecurity, r1.IsHighestSecurity,
            r2?.FormattedSecurity, r2?.IsHighestSecurity ?? false,
            r3?.FormattedSecurity, r3?.IsHighestSecurity ?? false));

        ComparisonRows.Add(CreateRow("amenities", "✨", "Tiện ích chính",
            r1.AmenitiesSummary, false,
            r2?.AmenitiesSummary, false,
            r3?.AmenitiesSummary, false));

        ComparisonRows.Add(CreateRow("quietHours", "🌙", "Giờ giấc",
            r1.QuietHours, false,
            r2?.QuietHours, false,
            r3?.QuietHours, false));

        ComparisonRows.Add(CreateRow("verified", "✅", "Kiểm duyệt",
            r1.VerifiedText, false,
            r2?.VerifiedText, false,
            r3?.VerifiedText, false));

        if (evaluateHighlights)
        {
            var cheapest = ComparedRooms.OrderBy(r => r.Rent).First();
            var closest = ComparedRooms.OrderBy(r => r.DistanceMeters).First();
            BestValueSummaryText = $"💡 Rẻ nhất: {cheapest.ShortTitle} ({cheapest.FormattedRent}) • Gần ICTU nhất: {closest.ShortTitle} ({closest.FormattedDistance})";
        }
        else
        {
            BestValueSummaryText = string.Empty;
        }

        NotifySlotAndSummaryProperties();
    }

    private ComparisonAttributeRowModel CreateRow(
        string key,
        string icon,
        string label,
        string? v1,
        bool win1,
        string? v2,
        bool win2,
        string? v3,
        bool win3)
    {
        return new ComparisonAttributeRowModel
        {
            AttributeKey = key,
            Icon = icon,
            AttributeLabel = label,
            Value1 = v1 ?? "-",
            Value2 = v2 ?? "-",
            Value3 = v3 ?? "-",
            HasSlot2 = Slot2 is not null,
            HasSlot3 = Slot3 is not null,
            Slot1BgHex = win1 ? WinnerBgHex : NormalBgHex,
            Slot2BgHex = win2 ? WinnerBgHex : NormalBgHex,
            Slot3BgHex = win3 ? WinnerBgHex : NormalBgHex,
            Slot1TextHex = win1 ? WinnerTextHex : NormalTextHex,
            Slot2TextHex = win2 ? WinnerTextHex : NormalTextHex,
            Slot3TextHex = win3 ? WinnerTextHex : NormalTextHex
        };
    }

    private void NotifySlotAndSummaryProperties()
    {
        OnPropertyChanged(nameof(Slot1));
        OnPropertyChanged(nameof(Slot2));
        OnPropertyChanged(nameof(Slot3));
        OnPropertyChanged(nameof(HasSlot1));
        OnPropertyChanged(nameof(HasSlot2));
        OnPropertyChanged(nameof(HasSlot3));
        OnPropertyChanged(nameof(HasComparedRooms));
        OnPropertyChanged(nameof(HasNoComparedRooms));
        OnPropertyChanged(nameof(CanAddMoreRooms));
        OnPropertyChanged(nameof(HasBestValueSummary));
        OnPropertyChanged(nameof(CompareCountText));
    }
}
