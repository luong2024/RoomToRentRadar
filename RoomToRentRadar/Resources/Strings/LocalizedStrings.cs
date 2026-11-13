using System.ComponentModel;
using System.Globalization;

namespace RoomToRentRadar.Resources.Strings;

public sealed class LocalizedStrings : INotifyPropertyChanged
{
    private static readonly LocalizedStrings _instance = new();
    public static LocalizedStrings Instance => _instance;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => AppResources.GetString(key);

    public void SetCulture(CultureInfo culture)
    {
        AppResources.Culture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

