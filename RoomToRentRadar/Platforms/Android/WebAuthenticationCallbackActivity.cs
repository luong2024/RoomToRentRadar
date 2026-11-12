using Android.App;
using Android.Content;
using Android.Content.PM;

namespace RoomToRentRadar;

/// <summary>
/// Handles the OAuth2 redirect URI callback from Chrome Custom Tabs back to the MAUI Android app.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes =
    [
        "com.googleusercontent.apps.69901634703-vt9nmqanpda8i8ai0qpkc3viqnfje6bb",
        "com.companyname.roomtorentradar"
    ],
    DataPath = "/oauth2redirect")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
