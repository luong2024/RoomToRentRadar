using RoomToRentRadar.Pages.Controls.RoomDetail.Models;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// MAUI implementation of <see cref="IRoomDetailDeviceActions"/>.
/// </summary>
public sealed class MauiRoomDetailDeviceActions : IRoomDetailDeviceActions
{
    public Task ShareTextAsync(string title, string text) =>
        Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = title,
            Text = text
        });

    public Task DialPhoneAsync(string phoneNumber)
    {
        if (PhoneDialer.Default.IsSupported)
        {
            PhoneDialer.Default.Open(phoneNumber);
            return Task.CompletedTask;
        }

        return Launcher.Default.OpenAsync($"tel:{phoneNumber}");
    }

    public Task OpenUriAsync(string uri) =>
        Launcher.Default.OpenAsync(uri);

    public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel) =>
        Shell.Current is null
            ? Task.FromResult(false)
            : Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    public Task DisplayToastAsync(string message) =>
        Shell.Current is null
            ? Task.CompletedTask
            : AppShell.DisplayToastAsync(message);
}
