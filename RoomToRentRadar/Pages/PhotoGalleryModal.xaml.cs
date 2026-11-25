using System.Collections.Generic;
using Microsoft.Maui.Controls;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class PhotoGalleryModal : ContentPage, IQueryAttributable
{
    private readonly PhotoGalleryViewModel _viewModel;
    private bool _loadedFromQuery;

    public PhotoGalleryModal(PhotoGalleryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("listingId", out var raw) && raw is string id && !string.IsNullOrWhiteSpace(id))
        {
            _loadedFromQuery = true;
            _ = _viewModel.LoadPhotosAsync(id);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loadedFromQuery && _viewModel.Photos.Count == 0 && !_viewModel.IsLoading)
        {
            await _viewModel.LoadPhotosAsync();
        }
    }
}
