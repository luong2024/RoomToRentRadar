using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace RoomToRentRadar.Services;

public sealed class MauiAppNavigationService : IAppNavigationService
{
    public async Task NavigateToRoomDetailAsync(string roomId, bool? isAvailable = null)
    {
        if (Shell.Current is null || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        var route = $"roomdetail?id={Uri.EscapeDataString(roomId)}";
        if (isAvailable.HasValue)
        {
            route += $"&isAvailable={isAvailable.Value}";
        }

        await Shell.Current.GoToAsync(route);
    }

    public async Task ShowAlertAsync(string title, string message, string cancel = "Đóng")
    {
        if (Shell.Current is null)
        {
            return;
        }

        await Shell.Current.DisplayAlertAsync(title, message, cancel);
    }
}
