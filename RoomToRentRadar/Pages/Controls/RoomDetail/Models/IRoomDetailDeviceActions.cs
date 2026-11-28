namespace RoomToRentRadar.Pages.Controls.RoomDetail.Models;

/// <summary>
/// Platform and UI dialog abstraction for <c>RoomDetailPageModel</c> (DIP: decouples PageModel from MAUI static singletons).
/// </summary>
public interface IRoomDetailDeviceActions
{
    Task ShareTextAsync(string title, string text);

    Task DialPhoneAsync(string phoneNumber);

    Task OpenUriAsync(string uri);

    Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel);

    Task DisplayToastAsync(string message);
}
