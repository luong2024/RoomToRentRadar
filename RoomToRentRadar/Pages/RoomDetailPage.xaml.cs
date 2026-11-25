using System.Collections.Generic;
using Microsoft.Maui.Controls;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RoomDetailPage : ContentPage, IQueryAttributable
{
    private readonly RoomDetailViewModel _viewModel;

    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public RoomDetailPage() : this(new RoomDetailViewModel())
    {
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _ = _viewModel.ApplyNavigationParametersAsync(query);
    }
}
