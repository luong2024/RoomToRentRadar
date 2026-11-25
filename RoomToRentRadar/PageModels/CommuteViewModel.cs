using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// ViewModel hiển thị thời gian di chuyển (Xe máy, Xe buýt, Đi bộ) và khoảng cách đường chim bay dự phòng (US09 T09.2).
/// </summary>
public partial class CommuteViewModel : ObservableObject
{
    private readonly ICommuteService _commuteService;
    private bool _hasInitialized;

    private CampusDestinationOption? _selectedCampus;
    private string _listingId = "room-ictu-001";
    private double? _roomLatitude;
    private double? _roomLongitude;
    private bool _isLoading;
    private bool _isFallbackStraightLine;
    private bool _simulateFallback;
    private int _motorbikeMinutes;
    private int _busMinutes;
    private int _walkMinutes;
    private double _routeDistanceKm;
    private double _straightLineKm;
    private string _errorMessage = string.Empty;

    public CommuteViewModel(ICommuteService commuteService)
    {
        _commuteService = commuteService;

        var destinations = _commuteService.GetCampusDestinations();
        foreach (var campus in destinations)
        {
            Campuses.Add(campus);
        }

        _selectedCampus = Campuses.FirstOrDefault();
    }

    public ObservableCollection<CampusDestinationOption> Campuses { get; } = [];

    public CampusDestinationOption? SelectedCampus
    {
        get => _selectedCampus;
        set
        {
            if (SetProperty(ref _selectedCampus, value) && _hasInitialized && value is not null)
            {
                _ = LoadCommuteAsync();
            }
        }
    }

    public string ListingId
    {
        get => _listingId;
        set => SetProperty(ref _listingId, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsFallbackStraightLine
    {
        get => _isFallbackStraightLine;
        set
        {
            if (SetProperty(ref _isFallbackStraightLine, value))
            {
                OnPropertyChanged(nameof(ShowTransportTimes));
                OnPropertyChanged(nameof(FallbackDistanceText));
            }
        }
    }

    public bool SimulateFallback
    {
        get => _simulateFallback;
        set
        {
            if (SetProperty(ref _simulateFallback, value))
            {
                OnPropertyChanged(nameof(FallbackToggleButtonText));
            }
        }
    }

    public int MotorbikeMinutes
    {
        get => _motorbikeMinutes;
        set
        {
            if (SetProperty(ref _motorbikeMinutes, value))
            {
                OnPropertyChanged(nameof(MotorbikeText));
            }
        }
    }

    public int BusMinutes
    {
        get => _busMinutes;
        set
        {
            if (SetProperty(ref _busMinutes, value))
            {
                OnPropertyChanged(nameof(BusText));
            }
        }
    }

    public int WalkMinutes
    {
        get => _walkMinutes;
        set
        {
            if (SetProperty(ref _walkMinutes, value))
            {
                OnPropertyChanged(nameof(WalkText));
            }
        }
    }

    public double RouteDistanceKm
    {
        get => _routeDistanceKm;
        set
        {
            if (SetProperty(ref _routeDistanceKm, value))
            {
                OnPropertyChanged(nameof(RouteDistanceText));
            }
        }
    }

    public double StraightLineKm
    {
        get => _straightLineKm;
        set
        {
            if (SetProperty(ref _straightLineKm, value))
            {
                OnPropertyChanged(nameof(FallbackDistanceText));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool ShowTransportTimes => !IsFallbackStraightLine;
    public string MotorbikeText => $"Xe máy: ~{MotorbikeMinutes} phút";
    public string BusText => $"Xe buýt: ~{BusMinutes} phút";
    public string WalkText => $"Đi bộ: ~{WalkMinutes} phút";
    public string RouteDistanceText => $"Quãng đường ước tính: {RouteDistanceKm.ToString("0.##", CultureInfo.InvariantCulture)} km";
    public string FallbackDistanceText => $"Khoảng cách đường chim bay: {StraightLineKm.ToString("0.##", CultureInfo.InvariantCulture)} km";
    public string FallbackToggleButtonText => SimulateFallback
        ? "Đang bật chế độ Fallback (chuyển về bình thường)"
        : "Giả lập mất định tuyến (Test Fallback AC3)";

    public async Task LoadForListingAsync(
        string listingId,
        double? roomLat = null,
        double? roomLng = null,
        CancellationToken cancellationToken = default)
    {
        ListingId = string.IsNullOrWhiteSpace(listingId) ? "room-ictu-001" : listingId;
        _roomLatitude = roomLat;
        _roomLongitude = roomLng;
        await LoadCommuteAsync(cancellationToken);
    }

    [RelayCommand]
    public async Task LoadCommuteAsync(CancellationToken cancellationToken = default)
    {
        var campus = SelectedCampus ?? Campuses.FirstOrDefault();
        if (campus is null)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var dto = await _commuteService.GetCommuteEstimateAsync(
                ListingId,
                campus,
                _roomLatitude,
                _roomLongitude,
                SimulateFallback,
                cancellationToken);

            MotorbikeMinutes = dto.MotorbikeMinutes;
            BusMinutes = dto.BusMinutes;
            WalkMinutes = dto.WalkMinutes;
            RouteDistanceKm = dto.RouteDistanceKm;
            StraightLineKm = dto.StraightLineKm;
            IsFallbackStraightLine = dto.IsFallbackStraightLine;
            _hasInitialized = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Không thể tính thời gian di chuyển: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleSimulateFallbackAsync()
    {
        SimulateFallback = !SimulateFallback;
        await LoadCommuteAsync();
    }
}
