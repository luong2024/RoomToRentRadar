using System.Globalization;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels.Base;

public static class ErrorMessageMapper
{
    private const string AccountLockedCode = "ACCOUNT_LOCKED";
    private const string TimePlaceholder = "{time}";

    public static string Map(Exception? exception, CultureInfo? culture)
    {
        if (exception is not ApiException apiException)
            return GetFallback(culture);

        var code = apiException.ErrorCode;
        if (string.IsNullOrWhiteSpace(code))
            return GetFallback(culture);

        if (apiException.IsNetworkError)
            return GetLocalized("Error_NETWORK_ERROR", culture);

        if (string.Equals(code, AccountLockedCode, StringComparison.Ordinal))
        {
            var template = GetLocalized("Error_ACCOUNT_LOCKED", culture);
            var lockDuration = GetLocalized("Error_LockDuration", culture);
            return template.Replace(TimePlaceholder, lockDuration, StringComparison.Ordinal);
        }

        return GetOptional("Error_" + code, culture) ?? GetFallback(culture);
    }

    private static string GetFallback(CultureInfo? culture) =>
        GetLocalized("Error_Unexpected", culture);

    private static string GetLocalized(string key, CultureInfo? culture) =>
        GetOptional(key, culture) ?? AppResources.GetString(key, culture);

    private static string? GetOptional(string key, CultureInfo? culture) =>
        AppResources.ResourceManager.GetString(key, culture ?? AppResources.Culture);
}
