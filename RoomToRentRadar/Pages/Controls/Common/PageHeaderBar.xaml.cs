using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Common;

public partial class PageHeaderBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeaderBar), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeaderBar), string.Empty,
            propertyChanged: (b, o, n) => ((PageHeaderBar)b).UpdateHasSubtitle());

    public static readonly BindableProperty CanGoBackProperty =
        BindableProperty.Create(nameof(CanGoBack), typeof(bool), typeof(PageHeaderBar), false);

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(PageHeaderBar), null);

    public static readonly BindableProperty ActionIconProperty =
        BindableProperty.Create(nameof(ActionIcon), typeof(string), typeof(PageHeaderBar), string.Empty,
            propertyChanged: (b, o, n) => ((PageHeaderBar)b).UpdateHasAction());

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(PageHeaderBar), null);

    public static readonly BindableProperty HasSubtitleProperty =
        BindableProperty.Create(nameof(HasSubtitle), typeof(bool), typeof(PageHeaderBar), false);

    public static readonly BindableProperty HasActionProperty =
        BindableProperty.Create(nameof(HasAction), typeof(bool), typeof(PageHeaderBar), false);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        set => SetValue(CanGoBackProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public string ActionIcon
    {
        get => (string)GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public bool HasSubtitle
    {
        get => (bool)GetValue(HasSubtitleProperty);
        private set => SetValue(HasSubtitleProperty, value);
    }

    public bool HasAction
    {
        get => (bool)GetValue(HasActionProperty);
        private set => SetValue(HasActionProperty, value);
    }

    public PageHeaderBar()
    {
        InitializeComponent();
        UpdateHasSubtitle();
        UpdateHasAction();
    }

    private void UpdateHasSubtitle()
    {
        HasSubtitle = !string.IsNullOrWhiteSpace(Subtitle);
    }

    private void UpdateHasAction()
    {
        HasAction = !string.IsNullOrWhiteSpace(ActionIcon);
    }
}
