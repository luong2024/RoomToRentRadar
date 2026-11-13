using System.Globalization;
using System.Resources;

namespace RoomToRentRadar.Resources.Strings;

public static class AppResources
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("RoomToRentRadar.Resources.Strings.AppResources", typeof(AppResources).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string GetString(string name) =>
        ResourceManager.GetString(name, Culture) ?? name;

    public static string ShowOrHidePassword => GetString(nameof(ShowOrHidePassword));
    public static string SignInWithGoogle => GetString(nameof(SignInWithGoogle));
    public static string RegisterWithEmailPassword => GetString(nameof(RegisterWithEmailPassword));
    public static string PromptLandlordPhoneTitle => GetString(nameof(PromptLandlordPhoneTitle));
    public static string PromptLandlordPhoneMessage => GetString(nameof(PromptLandlordPhoneMessage));
    public static string Confirm => GetString(nameof(Confirm));
    public static string Cancel => GetString(nameof(Cancel));
    public static string PhoneNumberRequiredTitle => GetString(nameof(PhoneNumberRequiredTitle));
    public static string PhoneNumberRequiredMessage => GetString(nameof(PhoneNumberRequiredMessage));
    public static string Ok => GetString(nameof(Ok));
    public static string GoogleSignInErrorTitle => GetString(nameof(GoogleSignInErrorTitle));
    public static string SignInFailedTitle => GetString(nameof(SignInFailedTitle));
    public static string SessionStorageError => GetString(nameof(SessionStorageError));
    public static string GoogleNavError => GetString(nameof(GoogleNavError));
    public static string GoogleAuthFailed => GetString(nameof(GoogleAuthFailed));
    public static string ContinueWithGoogleStudent => GetString(nameof(ContinueWithGoogleStudent));
    public static string ContinueWithGoogleLandlord => GetString(nameof(ContinueWithGoogleLandlord));
    public static string OrSignInWithEmail => GetString(nameof(OrSignInWithEmail));
}

