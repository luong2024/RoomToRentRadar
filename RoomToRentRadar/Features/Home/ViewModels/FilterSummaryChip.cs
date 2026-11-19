using System.Windows.Input;

namespace RoomToRentRadar.Features.Home.ViewModels;

public sealed record FilterSummaryChip(string Label)
{
    public ICommand? RemoveCommand { get; init; }
    public string DisplayLabel => $"{Label}  ×";
    public string AccessibilityLabel => $"Xóa bộ lọc {Label}";
}
