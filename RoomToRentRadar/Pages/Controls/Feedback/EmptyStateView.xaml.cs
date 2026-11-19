using System.Windows.Input;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class EmptyStateView : ContentView
{
    private string _defaultTitle = string.Empty;
    private string _defaultMessage = string.Empty;
    private string _defaultActionText = string.Empty;

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateView), null);

    public static readonly BindableProperty ActionCommandParameterProperty =
        BindableProperty.Create(nameof(ActionCommandParameter), typeof(object), typeof(EmptyStateView), null);

    public static readonly BindableProperty HasActionProperty =
        BindableProperty.Create(nameof(HasAction), typeof(bool), typeof(EmptyStateView), true);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => (object?)GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    public bool HasAction
    {
        get => (bool)GetValue(HasActionProperty);
        set => SetValue(HasActionProperty, value);
    }

    public EmptyStateView()
    {
        InitializeComponent();
        ApplyLocalizedDefaults();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        ApplyLocalizedDefaults();
        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsChanged;
    }

    private void OnLocalizedStringsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ApplyLocalizedDefaults();
    }

    // Only refresh values the caller has not overridden, so explicit texts survive language switches (US15).
    private void ApplyLocalizedDefaults()
    {
        if (Title == _defaultTitle)
        {
            _defaultTitle = LocalizedStrings.Instance["State_Empty_Title"];
            Title = _defaultTitle;
        }

        if (Message == _defaultMessage)
        {
            _defaultMessage = LocalizedStrings.Instance["State_Empty_Message"];
            Message = _defaultMessage;
        }

        if (ActionText == _defaultActionText)
        {
            _defaultActionText = LocalizedStrings.Instance["State_Empty_Action"];
            ActionText = _defaultActionText;
        }
    }
}

