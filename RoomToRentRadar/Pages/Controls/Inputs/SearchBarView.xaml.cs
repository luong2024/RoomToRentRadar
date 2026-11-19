using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Inputs;

public partial class SearchBarView : ContentView
{
    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(SearchBarView), string.Empty,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, o, n) => ((SearchBarView)b).UpdateClearButtonVisibility());

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchBarView), "Search rooms near ICTU...");

    public static readonly BindableProperty SearchCommandProperty =
        BindableProperty.Create(nameof(SearchCommand), typeof(ICommand), typeof(SearchBarView), null);

    public static readonly BindableProperty FilterCommandProperty =
        BindableProperty.Create(nameof(FilterCommand), typeof(ICommand), typeof(SearchBarView), null);

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public ICommand? FilterCommand
    {
        get => (ICommand?)GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }

    public SearchBarView()
    {
        InitializeComponent();
        UpdateClearButtonVisibility();
    }

    private void UpdateClearButtonVisibility()
    {
        ClearSearchBtn.IsVisible = !string.IsNullOrEmpty(SearchText);
    }

    private void ClearSearchBtn_Clicked(object? sender, EventArgs e)
    {
        SearchText = string.Empty;
        SearchInput.Focus();
        SearchCommand?.Execute(string.Empty);
    }
}
