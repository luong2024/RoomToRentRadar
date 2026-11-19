using System.Windows.Input;
using RoomToRentRadar.PageModels.Base;
using ViewState = RoomToRentRadar.PageModels.Base.ViewState;

namespace RoomToRentRadar.Pages.Controls.Feedback;

[ContentProperty(nameof(BodyContent))]
public partial class StateView : ContentView
{
    public static readonly BindableProperty StateProperty =
        BindableProperty.Create(nameof(State), typeof(ViewState), typeof(StateView), ViewState.Loading,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyState());

    public static readonly BindableProperty HasContentProperty =
        BindableProperty.Create(nameof(HasContent), typeof(bool), typeof(StateView), false,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyState());

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(StateView), string.Empty);

    public static readonly BindableProperty ErrorTitleProperty =
        BindableProperty.Create(nameof(ErrorTitle), typeof(string), typeof(StateView), string.Empty,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyErrorTitle((string)n));

    public static readonly BindableProperty RetryCommandProperty =
        BindableProperty.Create(nameof(RetryCommand), typeof(ICommand), typeof(StateView), null);

    public static readonly BindableProperty EmptyTitleProperty =
        BindableProperty.Create(nameof(EmptyTitle), typeof(string), typeof(StateView), string.Empty,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyEmptyTitle((string)n));

    public static readonly BindableProperty EmptyMessageProperty =
        BindableProperty.Create(nameof(EmptyMessage), typeof(string), typeof(StateView), string.Empty,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyEmptyMessage((string)n));

    public static readonly BindableProperty EmptyActionTextProperty =
        BindableProperty.Create(nameof(EmptyActionText), typeof(string), typeof(StateView), string.Empty,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyEmptyActionText((string)n));

    public static readonly BindableProperty EmptyActionCommandProperty =
        BindableProperty.Create(nameof(EmptyActionCommand), typeof(ICommand), typeof(StateView), null,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyEmptyActionCommand((ICommand?)n));

    public static readonly BindableProperty EmptyGlyphProperty =
        BindableProperty.Create(nameof(EmptyGlyph), typeof(string), typeof(StateView), string.Empty,
            propertyChanged: (b, o, n) => ((StateView)b).ApplyEmptyGlyph((string)n));

    public static readonly BindableProperty BodyContentProperty =
        BindableProperty.Create(nameof(BodyContent), typeof(View), typeof(StateView), null,
            propertyChanged: (b, o, n) => ((StateView)b).ContentHost.Content = (View?)n);

    public ViewState State
    {
        get => (ViewState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public bool HasContent
    {
        get => (bool)GetValue(HasContentProperty);
        set => SetValue(HasContentProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string ErrorTitle
    {
        get => (string)GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public string EmptyTitle
    {
        get => (string)GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public string EmptyActionText
    {
        get => (string)GetValue(EmptyActionTextProperty);
        set => SetValue(EmptyActionTextProperty, value);
    }

    public ICommand? EmptyActionCommand
    {
        get => (ICommand?)GetValue(EmptyActionCommandProperty);
        set => SetValue(EmptyActionCommandProperty, value);
    }

    public string EmptyGlyph
    {
        get => (string)GetValue(EmptyGlyphProperty);
        set => SetValue(EmptyGlyphProperty, value);
    }

    public View? BodyContent
    {
        get => (View?)GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    public StateView()
    {
        InitializeComponent();
        EmptyHost.HasAction = EmptyActionCommand is not null;
        ApplyState();
    }

    private void ApplyState()
    {
        var layout = StateLayout.Resolve(State, HasContent);
        BannerHost.IsVisible = layout.ShowBanner;
        SkeletonHost.IsVisible = layout.ShowSkeleton;
        ContentHost.IsVisible = layout.ShowContent;
        EmptyHost.IsVisible = layout.ShowEmpty;
        FullErrorHost.IsVisible = layout.ShowFullError;
        InlineErrorHost.IsVisible = layout.ShowInlineError;
    }

    // Empty pass-through values keep the child control's localized defaults when not overridden (US15).
    private void ApplyErrorTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return;

        FullErrorHost.Title = title;
        InlineErrorHost.Title = title;
    }

    private void ApplyEmptyTitle(string title)
    {
        if (!string.IsNullOrEmpty(title))
            EmptyHost.Title = title;
    }

    private void ApplyEmptyMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
            EmptyHost.Message = message;
    }

    private void ApplyEmptyActionText(string actionText)
    {
        if (!string.IsNullOrEmpty(actionText))
            EmptyHost.ActionText = actionText;
    }

    private void ApplyEmptyActionCommand(ICommand? command)
    {
        EmptyHost.ActionCommand = command;
        EmptyHost.HasAction = command is not null;
    }

    private void ApplyEmptyGlyph(string glyph)
    {
        if (!string.IsNullOrEmpty(glyph))
            EmptyHost.IconGlyph = glyph;
    }
}