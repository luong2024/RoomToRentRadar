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
    public static string PromptCreatePasswordTitle => GetString(nameof(PromptCreatePasswordTitle));
    public static string PromptCreatePasswordMessage => GetString(nameof(PromptCreatePasswordMessage));
    public static string PromptCurrentPasswordTitle => GetString(nameof(PromptCurrentPasswordTitle));
    public static string PromptCurrentPasswordMessage => GetString(nameof(PromptCurrentPasswordMessage));
    public static string PasswordRequiredTitle => GetString(nameof(PasswordRequiredTitle));
    public static string PasswordValidationRule => GetString(nameof(PasswordValidationRule));
    public static string GoogleClientIdMissing => GetString(nameof(GoogleClientIdMissing));
    public static string PromptLandlordHouseNameTitle => GetString(nameof(PromptLandlordHouseNameTitle));
    public static string PromptLandlordHouseNameMessage => GetString(nameof(PromptLandlordHouseNameMessage));
    public static string HouseNameRequiredTitle => GetString(nameof(HouseNameRequiredTitle));
    public static string HouseNameRequiredMessage => GetString(nameof(HouseNameRequiredMessage));
    public static string HouseNameLabel => GetString(nameof(HouseNameLabel));
    public static string HouseNamePlaceholder => GetString(nameof(HouseNamePlaceholder));
    public static string HouseAddressLabel => GetString(nameof(HouseAddressLabel));
    public static string HouseAddressPlaceholder => GetString(nameof(HouseAddressPlaceholder));
    public static string HouseAddressRequiredError => GetString(nameof(HouseAddressRequiredError));
    public static string HouseAddressRequiredTitle => GetString(nameof(HouseAddressRequiredTitle));
    public static string HouseAddressRequiredMessage => GetString(nameof(HouseAddressRequiredMessage));
    public static string CompleteRegistration => GetString(nameof(CompleteRegistration));
    public static string RegisterButtonText => GetString(nameof(RegisterButtonText));
    public static string GoogleOnboardingTitle => GetString(nameof(GoogleOnboardingTitle));
    public static string PromptLandlordHouseAddressTitle => GetString(nameof(PromptLandlordHouseAddressTitle));
    public static string PromptLandlordHouseAddressMessage => GetString(nameof(PromptLandlordHouseAddressMessage));
    public static string InvalidCredentials => GetString(nameof(InvalidCredentials));
    public static string AccountLocked => GetString(nameof(AccountLocked));
    public static string AccountDisabled => GetString(nameof(AccountDisabled));
    public static string LandlordPendingApproval => GetString(nameof(LandlordPendingApproval));
    public static string AccountLockedOrSuspended => GetString(nameof(AccountLockedOrSuspended));
    public static string SignInFailedDefault => GetString(nameof(SignInFailedDefault));
    public static string EmailAlreadyInUse => GetString(nameof(EmailAlreadyInUse));
    public static string InvalidStudentCode => GetString(nameof(InvalidStudentCode));
    public static string InvalidRoleSelected => GetString(nameof(InvalidRoleSelected));
    public static string RegistrationFailedDefault => GetString(nameof(RegistrationFailedDefault));
    public static string RegistrationCheckInfo => GetString(nameof(RegistrationCheckInfo));
}

