using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

public partial class RoomListingPageModel(IRoomListingService service) : ObservableObject
{
    private const int PageSize = 20;
    private CancellationTokenSource? _request;
    private int _generation;
    private int _offset;
    private bool _initialized;
    public List<Campus> Campuses { get; } = [.. DemoRoomListings.Campuses];
    public ObservableCollection<RoomListingItem> Rooms { get; } = [];
    public string ResultsText => $"Đã tải {Rooms.Count} phòng · Dữ liệu minh họa";
    public bool CanLoadMore => HasMore && !IsLoading && !HasError && Rooms.Count > 0;
    partial void OnHasMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));
    private Campus _selectedCampus = DemoRoomListings.Campuses[0];
    public Campus SelectedCampus
    {
        get => _selectedCampus;
        set
        {
            // A native Picker may briefly deselect while its item source attaches.
            if (value is not null && SetProperty(ref _selectedCampus, value))
                RefreshCommand.Execute(null);
        }
    }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool HasMore { get; set; } = true;
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial string EmptyMessage { get; set; } = "Đang tải phòng trọ…";
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_initialized) await RefreshAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        _request?.Cancel();
        _request?.Dispose();
        var generation = ++_generation;
        _request = new CancellationTokenSource();
        _offset = 0;
        Rooms.Clear();
        OnPropertyChanged(nameof(ResultsText));
        HasMore = true;
        IsLoading = false;
        try { await LoadAsync(generation, _request.Token); }
        finally { if (generation == _generation) IsRefreshing = false; }
    }

    [RelayCommand]
    private Task LoadMoreAsync() => IsLoading || !HasMore || HasError
        ? Task.CompletedTask : LoadAsync(_generation, _request?.Token ?? default);

    [RelayCommand]
    private Task RetryAsync() => IsLoading ? Task.CompletedTask : LoadAsync(_generation, _request?.Token ?? default);

    private async Task LoadAsync(int generation, CancellationToken token)
    {
        IsLoading = true;
        ErrorMessage = "";
        EmptyMessage = "Đang tải phòng trọ…";
        try
        {
            var page = await service.GetPageAsync(SelectedCampus ?? Campuses[0], _offset, PageSize, token);
            if (generation != _generation || token.IsCancellationRequested) return;
            var ids = Rooms.Select(r => r.Room.Id).ToHashSet();
            foreach (var room in page.Items)
                if (ids.Add(room.Room.Id)) Rooms.Add(room);
            _offset += page.Items.Count;
            OnPropertyChanged(nameof(ResultsText));
            HasMore = page.HasMore;
            _initialized = true;
            EmptyMessage = "Không có phòng còn trống và đã xác minh gần campus này.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation != _generation) return;
            ErrorMessage = "Không thể tải danh sách. Vui lòng thử lại.";
            EmptyMessage = "Chưa tải được phòng trọ.";
        }
        finally { if (generation == _generation) IsLoading = false; }
    }
}
