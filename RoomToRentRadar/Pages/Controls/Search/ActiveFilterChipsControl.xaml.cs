using System.Collections;
using System.Windows.Input;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadar.Pages.Controls.Search;

/// <summary>Removable active-filter chips + "Clear all" for Explore (US04 AC3, UC-05 A1).</summary>
public partial class ActiveFilterChipsControl : ContentView
{
    public ActiveFilterChipsControl()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public static readonly BindableProperty ChipsProperty = BindableProperty.Create(
        nameof(Chips), typeof(IEnumerable<ActiveFilterChipModel>), typeof(ActiveFilterChipsControl));

    public static readonly BindableProperty HasActiveFiltersProperty = BindableProperty.Create(
        nameof(HasActiveFilters), typeof(bool), typeof(ActiveFilterChipsControl), false);

    public static readonly BindableProperty RemoveChipCommandProperty = BindableProperty.Create(
        nameof(RemoveChipCommand), typeof(ICommand), typeof(ActiveFilterChipsControl));

    public static readonly BindableProperty ClearAllCommandProperty = BindableProperty.Create(
        nameof(ClearAllCommand), typeof(ICommand), typeof(ActiveFilterChipsControl));

    public static readonly BindableProperty ClearAllTextProperty = BindableProperty.Create(
        nameof(ClearAllText), typeof(string), typeof(ActiveFilterChipsControl), string.Empty);

    public IEnumerable<ActiveFilterChipModel>? Chips
    {
        get => (IEnumerable<ActiveFilterChipModel>?)GetValue(ChipsProperty);
        set => SetValue(ChipsProperty, value);
    }

    public bool HasActiveFilters
    {
        get => (bool)GetValue(HasActiveFiltersProperty);
        set => SetValue(HasActiveFiltersProperty, value);
    }

    public ICommand? RemoveChipCommand
    {
        get => (ICommand?)GetValue(RemoveChipCommandProperty);
        set => SetValue(RemoveChipCommandProperty, value);
    }

    public ICommand? ClearAllCommand
    {
        get => (ICommand?)GetValue(ClearAllCommandProperty);
        set => SetValue(ClearAllCommandProperty, value);
    }

    public string ClearAllText
    {
        get => (string)GetValue(ClearAllTextProperty);
        set => SetValue(ClearAllTextProperty, value);
    }
}
