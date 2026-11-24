using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace RoomToRentRadar.Services.RoomListings;

public sealed class FavoriteFeedbackService : IFavoriteFeedbackService
{
    public async Task ShowMessageAsync(string message)
    {
        try
        {
            await Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(4),
                visualOptions: CreateSnackbarOptions()).Show();
        }
        catch (InvalidOperationException)
        {
            // Snackbar presentation is best-effort when there is no active page.
        }
    }

    public async Task ShowUndoAsync(string message, string actionText, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            var snackbar = Snackbar.Make(
                message,
                () => MainThread.BeginInvokeOnMainThread(async () => await action()),
                actionText,
                TimeSpan.FromSeconds(5),
                CreateSnackbarOptions());
            await snackbar.Show();
        }
        catch (InvalidOperationException)
        {
            // The persisted removal remains valid if no page can present the undo action.
        }
    }

    private static SnackbarOptions CreateSnackbarOptions()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return new SnackbarOptions
        {
            BackgroundColor = GetThemeColor(isDark ? "CardBackgroundDark" : "CardBackground", isDark ? Color.FromArgb("#1E293B") : Colors.White),
            TextColor = GetThemeColor(isDark ? "TextPrimarySlateDark" : "TextPrimarySlate", isDark ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#0F172A")),
            ActionButtonTextColor = GetThemeColor(isDark ? "PrimaryBrandDark" : "PrimaryBrand", isDark ? Color.FromArgb("#60A5FA") : Color.FromArgb("#2563EB")),
            Font = Microsoft.Maui.Font.SystemFontOfSize(14),
            ActionButtonFont = Microsoft.Maui.Font.SystemFontOfSize(14, FontWeight.Bold),
            CornerRadius = 14
        };
    }

    private static Color GetThemeColor(string key, Color fallback)
    {
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
    }
}
