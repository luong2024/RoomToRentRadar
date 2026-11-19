using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// Shared view model for every not-yet-built route (T00.15). One instance is configured with a
/// <see cref="PlaceholderEntry"/> from <see cref="PlaceholderCatalog"/>; the feature task that owns
/// the route later swaps in a real page without touching this type.
/// </summary>
[QueryProperty(nameof(Id), "id")]
public partial class PlaceholderPageModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private PlaceholderEntry? _entry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasId))]
    [NotifyPropertyChangedFor(nameof(IdText))]
    public partial string Id { get; set; } = string.Empty;

    public PlaceholderPageModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        LocalizedStrings.Instance.PropertyChanged += (_, _) => RefreshLocalizedTexts();
    }

    public bool IsConfigured => _entry != null;

    // my-listings and moderation are root tabs (bottom bar, no Back); the 12 detail routes are pushed.
    public bool IsRootTab => _entry?.Route is AppRoutes.MyListings or AppRoutes.Moderation;
    public bool CanGoBack => !IsRootTab;
    public string ActiveRoute => _entry?.Route ?? AppRoutes.Explore;

    public string Title => _entry is null ? string.Empty : LocalizedStrings.Instance[_entry.TitleKey];
    public string ScrId => _entry?.ScrId ?? string.Empty;
    public string ComingInText => _entry is null
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, LocalizedStrings.Instance["Placeholder_ComingIn"], _entry.TaskId);

    public bool HasId => !string.IsNullOrWhiteSpace(Id);
    public string IdText => HasId
        ? string.Format(CultureInfo.CurrentCulture, LocalizedStrings.Instance["Placeholder_IdFormat"], Id)
        : string.Empty;

    public void Configure(PlaceholderEntry entry)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        OnPropertyChanged(nameof(IsRootTab));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(ActiveRoute));
        OnPropertyChanged(nameof(ScrId));
        RefreshLocalizedTexts();
    }

    [RelayCommand]
    private async Task BackAsync() => await _navigation.GoBackAsync();

    private void RefreshLocalizedTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ComingInText));
        OnPropertyChanged(nameof(IdText));
    }
}