using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class ErrorRetryView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ErrorRetryView), "Connection Error");

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(ErrorRetryView),
            "Unable to connect to the server. Please check your internet connection.");

    public static readonly BindableProperty RetryTextProperty =
        BindableProperty.Create(nameof(RetryText), typeof(string), typeof(ErrorRetryView), "Retry");

    public static readonly BindableProperty RetryCommandProperty =
        BindableProperty.Create(nameof(RetryCommand), typeof(ICommand), typeof(ErrorRetryView), null);

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

    public ErrorRetryView()
    {
        InitializeComponent();
    }
}

