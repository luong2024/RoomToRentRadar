using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace RoomToRentRadar.Services;

public sealed class MauiNavigationService : INavigationService
{
    public async Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (Shell.Current is null)
            return;

        if (parameters is { Count: > 0 })
        {
            await Shell.Current.GoToAsync(route, new Dictionary<string, object>(parameters));
        }
        else
        {
            await Shell.Current.GoToAsync(route);
        }
    }

    public async Task GoBackAsync()
    {
        if (Shell.Current is null)
            return;

        await Shell.Current.GoToAsync("..");
    }
}
