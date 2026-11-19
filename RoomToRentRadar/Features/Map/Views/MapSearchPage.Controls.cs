using System.ComponentModel;
using Mapsui;
using Mapsui.Projections;

namespace RoomToRentRadar.Features.Map.Views;

public partial class MapSearchPage
{
    private const string OfflineNotice = "Bạn đang ngoại tuyến. Bản đồ và kết quả mới cần kết nối Internet; khu vực đã vẽ vẫn được giữ.";
    private const string LocatingNotice = "Đang xác định vị trí của bạn…";
    private CancellationTokenSource? _locationCts;
    private bool _sortSheetOpen;

    private void OnBaseMapPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(_baseMapLayer.Busy))
            Dispatcher.Dispatch(() =>
            {
                if (_observing)
                    _model.IsMapLoading = _baseMapLayer.Busy;
            });
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
        => Dispatcher.Dispatch(UpdateConnectivityNotice);

    private void UpdateConnectivityNotice()
    {
        if (!_observing)
            return;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            _model.MapNotice = OfflineNotice;
        else if (_model.MapNotice == OfflineNotice)
            _model.MapNotice = "";
    }

    private void OnFocusAreaClicked(object? sender, EventArgs args) => FocusDraft();
    private void OnDismissNoticeClicked(object? sender, EventArgs args) => _model.MapNotice = "";

    private async void OnSortClicked(object? sender, EventArgs args)
    {
        if (_sortSheetOpen)
            return;
        _sortSheetOpen = true;
        try
        {
            var options = _model.SortOptions.Select((option, index) =>
                index == _model.SelectedSortIndex ? $"✓ {option.Label}" : option.Label).ToArray();
            var selected = await DisplayActionSheetAsync("Sắp xếp phòng", "Hủy", null, options);
            var index = Array.IndexOf(options, selected);
            if (_observing && index >= 0)
                _model.SelectedSortIndex = index;
        }
        finally
        {
            _sortSheetOpen = false;
        }
    }

    private async void OnDeleteSavedAreaClicked(object? sender, EventArgs args)
    {
        if (_model.SelectedSavedArea is not { } saved)
            return;
        if (await DisplayAlertAsync("Xóa khu vực đã lưu?", $"Khu vực “{saved.Name}” sẽ bị xóa khỏi danh sách đã lưu.", "Xóa khu vực", "Giữ lại"))
        {
            // The selected row may have changed while the confirmation was open.
            if (_model.SelectedSavedArea?.Id == saved.Id)
                _model.DeleteSavedAreaCommand.Execute(null);
        }
    }

    private async void OnLocateClicked(object? sender, EventArgs args)
    {
        if (_locationCts is not null)
            return;
        using var cts = new CancellationTokenSource();
        _locationCts = cts;
        LocateButton.IsEnabled = false;
        _model.MapNotice = LocatingNotice;
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (!_observing || cts.IsCancellationRequested)
                return;
            if (permission != PermissionStatus.Granted)
            {
                _model.MapNotice = "Chưa có quyền vị trí. Cấp quyền Vị trí cho Room Radar trong Cài đặt rồi thử lại; bạn vẫn có thể vẽ khu vực.";
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)), cts.Token);
            if (!_observing || cts.IsCancellationRequested)
                return;
            if (location is null)
            {
                _model.MapNotice = "Chưa xác định được vị trí. Hãy thử lại ở nơi có tín hiệu GPS tốt hơn.";
                return;
            }

            var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            MapView.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 4);
            _model.MapNotice = "Đã đến vị trí của bạn. Vẽ khu vực quanh đây để tìm phòng.";
        }
        catch (OperationCanceledException) { }
        catch (FeatureNotEnabledException)
        {
            if (_observing)
                _model.MapNotice = "GPS đang tắt. Bật dịch vụ Vị trí trên thiết bị rồi nhấn định vị lại.";
        }
        catch (FeatureNotSupportedException)
        {
            if (_observing)
                _model.MapNotice = "Thiết bị này chưa hỗ trợ định vị. Di chuyển bản đồ và vẽ khu vực bạn muốn tìm.";
        }
        catch (PermissionException)
        {
            if (_observing)
                _model.MapNotice = "Chưa có quyền vị trí. Cấp quyền trong Cài đặt hoặc tiếp tục vẽ khu vực trên bản đồ.";
        }
        catch (Exception)
        {
            if (_observing)
                _model.MapNotice = "Chưa thể lấy vị trí hiện tại. Kiểm tra GPS và kết nối rồi thử lại.";
        }
        finally
        {
            if (_model.MapNotice == LocatingNotice)
                _model.MapNotice = "";
            _locationCts = null;
            LocateButton.IsEnabled = true;
        }
    }
}
