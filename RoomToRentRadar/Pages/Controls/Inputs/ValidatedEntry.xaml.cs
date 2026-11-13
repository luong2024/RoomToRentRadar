namespace RoomToRentRadar.Pages.Controls.Inputs;

public partial class ValidatedEntry : ContentView
{
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(ValidatedEntry), string.Empty,
            propertyChanged: (b, o, n) => ((ValidatedEntry)b).UpdateHasLabel());

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ValidatedEntry), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ValidatedEntry), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(ValidatedEntry), false,
            propertyChanged: (b, o, n) => ((ValidatedEntry)b).UpdatePasswordVisibility((bool)n));

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(ValidatedEntry), Keyboard.Default);

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(ValidatedEntry), string.Empty,
            propertyChanged: (b, o, n) => ((ValidatedEntry)b).UpdateErrorState());

    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(ValidatedEntry), false,
            propertyChanged: (b, o, n) => ((ValidatedEntry)b).UpdateBorderStyle());

    public static readonly BindableProperty HasLabelProperty =
        BindableProperty.Create(nameof(HasLabel), typeof(bool), typeof(ValidatedEntry), false);

    private bool _isPasswordHidden = true;

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public bool HasLabel
    {
        get => (bool)GetValue(HasLabelProperty);
        private set => SetValue(HasLabelProperty, value);
    }

    public ValidatedEntry()
    {
        InitializeComponent();
        UpdateHasLabel();
        UpdatePasswordVisibility(IsPassword);
        UpdateBorderStyle();
    }

    private void UpdateHasLabel()
    {
        HasLabel = !string.IsNullOrWhiteSpace(LabelText);
    }

    private void UpdatePasswordVisibility(bool isPassword)
    {
        _isPasswordHidden = isPassword;
        InnerEntry.IsPassword = isPassword;
        if (PasswordToggleButton != null)
        {
            PasswordToggleButton.IsVisible = isPassword;
        }
        if (PasswordToggleIcon != null)
        {
            PasswordToggleIcon.Glyph = Fonts.FluentUI.eye_20_regular;
        }
    }

    private void PasswordToggleButton_Clicked(object? sender, EventArgs e)
    {
        _isPasswordHidden = !_isPasswordHidden;
        InnerEntry.IsPassword = _isPasswordHidden;

        if (PasswordToggleIcon != null)
        {
            PasswordToggleIcon.Glyph = _isPasswordHidden
                ? Fonts.FluentUI.eye_20_regular
                : Fonts.FluentUI.eye_off_20_regular;
        }
    }

    private void UpdateErrorState()
    {
        HasError = !string.IsNullOrWhiteSpace(ErrorMessage);
        UpdateBorderStyle();
    }

    private void InnerEntry_Focused(object? sender, FocusEventArgs e)
    {
        UpdateBorderStyle(isFocused: true);
    }

    private void InnerEntry_Unfocused(object? sender, FocusEventArgs e)
    {
        UpdateBorderStyle(isFocused: false);
    }

    private void UpdateBorderStyle(bool isFocused = false)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources == null) return;
        var res = Microsoft.Maui.Controls.Application.Current.Resources;

        if (HasError)
        {
            EntryBorder.Stroke = res.TryGetValue("StatusDangerLight", out var dangerColor) ? (Color)dangerColor : Colors.Red;
            EntryBorder.StrokeThickness = 2.0;
        }
        else if (isFocused)
        {
            EntryBorder.Stroke = res.TryGetValue("BorderFocusLight", out var focusColor) ? (Color)focusColor : Color.FromArgb("#256D85");
            EntryBorder.StrokeThickness = 2.0;
        }
        else
        {
            EntryBorder.Stroke = res.TryGetValue("BorderSubtleLight", out var subtleColor) ? (Color)subtleColor : Color.FromArgb("#E2E8F0");
            EntryBorder.StrokeThickness = 1.0;
        }
    }
}
