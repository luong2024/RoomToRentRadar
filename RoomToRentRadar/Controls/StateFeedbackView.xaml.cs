using System.Windows.Input;

namespace RoomToRentRadar.Controls;

public partial class StateFeedbackView : ContentView
{
    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(
        nameof(IconSource), typeof(string), typeof(StateFeedbackView), "search_icon.png");

    public static readonly BindableProperty EyebrowTextProperty = BindableProperty.Create(
        nameof(EyebrowText), typeof(string), typeof(StateFeedbackView), string.Empty);

    public static readonly BindableProperty TitleTextProperty = BindableProperty.Create(
        nameof(TitleText), typeof(string), typeof(StateFeedbackView), string.Empty);

    public static readonly BindableProperty SubtitleTextProperty = BindableProperty.Create(
        nameof(SubtitleText), typeof(string), typeof(StateFeedbackView), string.Empty);

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(
        nameof(ActionText), typeof(string), typeof(StateFeedbackView), string.Empty);

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(
        nameof(ActionCommand), typeof(ICommand), typeof(StateFeedbackView));

    public StateFeedbackView() => InitializeComponent();

    public string IconSource
    {
        get => (string)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string EyebrowText
    {
        get => (string)GetValue(EyebrowTextProperty);
        set => SetValue(EyebrowTextProperty, value);
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string SubtitleText
    {
        get => (string)GetValue(SubtitleTextProperty);
        set => SetValue(SubtitleTextProperty, value);
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

    public bool HasEyebrow => !string.IsNullOrWhiteSpace(EyebrowText);

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(EyebrowText))
            OnPropertyChanged(nameof(HasEyebrow));
        if (propertyName == nameof(ActionText))
            OnPropertyChanged(nameof(HasAction));
    }
}
