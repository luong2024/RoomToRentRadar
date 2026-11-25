using System.Threading.Tasks;

namespace RoomToRentRadar.Services;

/// <summary>
/// Trừu tượng hóa điều hướng và hộp thoại thông báo để ViewModel có thể unit test độc lập.
/// </summary>
public interface IAppNavigationService
{
    Task NavigateToRoomDetailAsync(string roomId, bool? isAvailable = null);

    Task ShowAlertAsync(string title, string message, string cancel = "Đóng");
}
