namespace RoomToRentRadar.Pages;

public partial class CampusPickerPage : ContentPage
{
    private readonly PageModels.CampusPickerPageModel _viewModel;

    public CampusPickerPage(PageModels.CampusPickerPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SearchEntry.HandlerChanged += OnSearchEntryHandlerChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    // The design shows a bare input inside the outlined field, so drop the Android underline.
    private void OnSearchEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (SearchEntry.Handler?.PlatformView is Android.Widget.EditText editText)
        {
            editText.Background = null;
            editText.SetPadding(0, 0, 0, 0);
        }
#endif
    }
}
