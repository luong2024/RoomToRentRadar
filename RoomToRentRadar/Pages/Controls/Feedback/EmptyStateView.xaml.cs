using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(EmptyStateView), "🔍");

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), "Không tìm thấy phòng trọ phù hợp");

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyStateView),
            "Hãy thử giảm bớt tiêu chí lọc hoặc mở rộng bán kính tìm kiếm quanh ICTU.");

    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(EmptyStateView), "Xóa bộ lọc");

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
    }
}

