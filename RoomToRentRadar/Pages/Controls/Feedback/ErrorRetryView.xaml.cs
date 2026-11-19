using System.Windows.Input;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class ErrorRetryView : ContentView
{
    private string _defaultTitle = string.Empty;
    private string _defaultErrorMessage = string.Empty;
    private string _defaultRetryText = string.Empty;

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ErrorRetryView), string.Empty);

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(ErrorRetryView), string.Empty);

    public static readonly BindableProperty RetryTextProperty =
        BindableProperty.Create(nameof(RetryText), typeof(string), typeof(ErrorRetryView), string.Empty);

    public static readonly BindableProperty RetryCommandProperty =
        BindableProperty.Create(nameof(RetryCommand), typeof(ICommand), typeof(ErrorRetryView), null);

    public static readonly BindableProperty CompactProperty =
        BindableProperty.Create(nameof(Compact), typeof(bool), typeof(ErrorRetryView), false,
            propertyChanged: (b, o, n) => ((ErrorRetryView)b).UpdateLayoutVariant());

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string RetryText
    {
        get => (string)GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public ErrorRetryView()
    {
        InitializeComponent();
        ApplyLocalizedDefaults();
        UpdateLayoutVariant();
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

    private void UpdateLayoutVariant()
    {
        FullLayout.IsVisible = !Compact;
        CompactLayout.IsVisible = Compact;
    }

    // Only refresh values the caller has not overridden, so explicit texts survive language switches (US15).
    private void ApplyLocalizedDefaults()
    {
        if (Title == _defaultTitle)
        {
            _defaultTitle = LocalizedStrings.Instance["State_Error_Title"];
            Title = _defaultTitle;
        }

        if (ErrorMessage == _defaultErrorMessage)
        {
            _defaultErrorMessage = LocalizedStrings.Instance["State_Error_Message"];
            ErrorMessage = _defaultErrorMessage;
        }

        if (RetryText == _defaultRetryText)
        {
            _defaultRetryText = LocalizedStrings.Instance["State_Retry"];
            RetryText = _defaultRetryText;
        }
    }
}

