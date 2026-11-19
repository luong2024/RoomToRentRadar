using System.ComponentModel;
using System.Globalization;

namespace RoomToRentRadar.Resources.Strings;

public class LocalizedStrings : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizedStrings> _lazyInstance = new(() => new LocalizedStrings());
    public static LocalizedStrings Instance => _lazyInstance.Value;

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return AppResources.GetString(key);
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        AppResources.Culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

#if !UNIT_TESTS
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public class LocalizedStringsExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var path = string.IsNullOrWhiteSpace(Key) ? "." : $"[{Key}]";
        return new Binding(path, source: LocalizedStrings.Instance);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
#endif
