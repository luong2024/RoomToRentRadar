using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

/// <summary>Selectable room-type chip for the listing editor.</summary>
public partial class RoomTypeOption : ObservableObject
{
    private readonly Action<RoomTypeOption> _onSelected;

    public RoomTypeOption(string label, RoomType value, Action<RoomTypeOption> onSelected)
    {
        Label = label;
        Value = value;
        _onSelected = onSelected;
    }

    public string Label { get; }

    public RoomType Value { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => _onSelected(this);
}
