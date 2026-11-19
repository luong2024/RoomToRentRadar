using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.PageModels;

public partial class MessagesPageModel : ObservableObject
{
    private readonly IAppSession _appSession;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAuthenticated))]
    [NotifyPropertyChangedFor(nameof(IsPromptVisible))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    public partial bool IsGuest { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPromptVisible))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    public partial bool IsPromptDismissed { get; set; }

    public bool IsAuthenticated => !IsGuest;

    // BR-AUTH-07: guests see an in-place sign-in invitation instead of the unread banner (T00.15).
    public bool IsPromptVisible => IsGuest && !IsPromptDismissed;
    public bool IsContentVisible => !IsPromptVisible;

    public string PageTitle => AppResources.GetString("Messages_Title");
    public string PageSubtitle => AppResources.GetString("Messages_Subtitle");
    public string UnreadBannerTitle => AppResources.GetString("Messages_UnreadBannerTitle");
    public string UnreadBannerSubtitle => AppResources.GetString("Messages_UnreadBannerSubtitle");
    public string EmptyTitle => AppResources.GetString("Messages_EmptyTitle");
    public string EmptyMessage => AppResources.GetString("Messages_EmptyMessage");
    public string PromptTitle => AppResources.GetString("Prompt_SignIn_Title");
    public string PromptMessage => AppResources.GetString("Prompt_SignIn_MessagesMessage");
    public string PromptSignInText => AppResources.GetString("Prompt_SignIn_Button");
    public string PromptNotNowText => AppResources.GetString("Prompt_SignIn_NotNow");

    public MessagesPageModel(IAppSession appSession, INavigationService navigation)
    {
        _appSession = appSession ?? throw new ArgumentNullException(nameof(appSession));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        LocalizedStrings.Instance.PropertyChanged += (_, _) => RefreshLocalizedTexts();
    }

    public void Refresh()
    {
        IsGuest = _appSession.CurrentRole is null;
        IsPromptDismissed = false;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        await _navigation.RequireSignInAsync("//" + AppRoutes.Messages);
    }

    [RelayCommand]
    private void DismissPrompt()
    {
        IsPromptDismissed = true;
    }

    private void RefreshLocalizedTexts()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(UnreadBannerTitle));
        OnPropertyChanged(nameof(UnreadBannerSubtitle));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(PromptTitle));
        OnPropertyChanged(nameof(PromptMessage));
        OnPropertyChanged(nameof(PromptSignInText));
        OnPropertyChanged(nameof(PromptNotNowText));
    }
}