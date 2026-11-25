using System.ComponentModel;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages;

public partial class UiCatalogPage : ContentPage
{
    private readonly UiCatalogPageModel _viewModel;
    private bool _isSubscribedToCultureChanges;

    public UiCatalogPage(UiCatalogPageModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isSubscribedToCultureChanges)
        {
            LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsPropertyChanged;
            _isSubscribedToCultureChanges = true;
        }

        _viewModel.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        if (_isSubscribedToCultureChanges)
        {
            LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsPropertyChanged;
            _isSubscribedToCultureChanges = false;
        }

        base.OnDisappearing();
    }

    private void OnLocalizedStringsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (MainThread.IsMainThread)
        {
            _viewModel.OnAppCultureChanged();
            return;
        }

        MainThread.BeginInvokeOnMainThread(_viewModel.OnAppCultureChanged);
    }
}
