using RoomToRentRadar.Core.Constants;

namespace RoomToRentRadar;

public partial class MainWindow : Window
{
    public MainWindow(Page rootPage)
    {
        InitializeComponent();
        Page = rootPage;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ClearSearchButton.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        await NavigateWithSearchAsync(TitleBarSearchEntry.Text);
    }

    private async void OnClearSearchTapped(object? sender, TappedEventArgs e)
    {
        TitleBarSearchEntry.Text = string.Empty;
        await NavigateWithSearchAsync(string.Empty);
    }

    private static async Task NavigateWithSearchAsync(string? rawQuery)
    {
        if (Shell.Current is null)
        {
            return;
        }

        try
        {
            var trimmed = rawQuery?.Trim() ?? string.Empty;
            var route = string.IsNullOrEmpty(trimmed)
                ? $"//{AppRoutes.RoomListings}"
                : $"//{AppRoutes.RoomListings}?search={Uri.EscapeDataString(trimmed)}";

            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TitleBar search navigation failed: {ex.Message}");
        }
    }
}
