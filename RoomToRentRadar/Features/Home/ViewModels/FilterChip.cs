using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => _onSelected(this);
}
