using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class FilterChip : ObservableObject
{
    private readonly Action<FilterChip> _onSelected;

    public FilterChip(string label, object? value, Action<FilterChip> onSelected)
    {
        Label = label;
        Value = value;
        _onSelected = onSelected;
    }

    public string Label { get; }

    public object? Value { get; }

    public string AccessibilityLabel => IsSelected ? $"{Label}, đã chọn" : $"{Label}, chưa chọn";

    public string ImageSource => Value switch
    {
        RoomType.SingleRoom => "cat_single.png",
        RoomType.SharedRoom => "cat_shared.png",
        RoomType.Studio => "cat_studio.png",
        RoomType.Apartment => "cat_apartment.png",
        RoomType.House => "cat_house.png",
        _ => "cat_all.png"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibilityLabel))]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => _onSelected(this);
}
