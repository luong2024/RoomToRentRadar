using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Search;

/// <summary>Filters button with active-count badge, Sort selector and the {N} rooms line (Explore).</summary>
public partial class ExploreFilterBarControl : ContentView
{
    public ExploreFilterBarControl()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public static readonly BindableProperty ActiveFilterCountProperty = BindableProperty.Create(
        nameof(ActiveFilterCount), typeof(int), typeof(ExploreFilterBarControl), 0);

    public static readonly BindableProperty HasActiveFiltersProperty = BindableProperty.Create(
        nameof(HasActiveFilters), typeof(bool), typeof(ExploreFilterBarControl), false);

    public static readonly BindableProperty CurrentSortLabelProperty = BindableProperty.Create(
        nameof(CurrentSortLabel), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty FiltersButtonTextProperty = BindableProperty.Create(
        nameof(FiltersButtonText), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty FiltersButtonDescriptionProperty = BindableProperty.Create(
        nameof(FiltersButtonDescription), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty SortButtonDescriptionProperty = BindableProperty.Create(
        nameof(SortButtonDescription), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty ResultCountTextProperty = BindableProperty.Create(
        nameof(ResultCountText), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty VerifiedOnlySuffixTextProperty = BindableProperty.Create(
        nameof(VerifiedOnlySuffixText), typeof(string), typeof(ExploreFilterBarControl), string.Empty);

    public static readonly BindableProperty OpenFiltersCommandProperty = BindableProperty.Create(
        nameof(OpenFiltersCommand), typeof(ICommand), typeof(ExploreFilterBarControl));

    public static readonly BindableProperty OpenSortCommandProperty = BindableProperty.Create(
        nameof(OpenSortCommand), typeof(ICommand), typeof(ExploreFilterBarControl));

    public int ActiveFilterCount
    {
        get => (int)GetValue(ActiveFilterCountProperty);
        set => SetValue(ActiveFilterCountProperty, value);
    }

    public bool HasActiveFilters
    {
        get => (bool)GetValue(HasActiveFiltersProperty);
        set => SetValue(HasActiveFiltersProperty, value);
    }

    public string CurrentSortLabel
    {
        get => (string)GetValue(CurrentSortLabelProperty);
        set => SetValue(CurrentSortLabelProperty, value);
    }

    public string FiltersButtonText
    {
        get => (string)GetValue(FiltersButtonTextProperty);
        set => SetValue(FiltersButtonTextProperty, value);
    }

    public string FiltersButtonDescription
    {
        get => (string)GetValue(FiltersButtonDescriptionProperty);
        set => SetValue(FiltersButtonDescriptionProperty, value);
    }

    public string SortButtonDescription
    {
        get => (string)GetValue(SortButtonDescriptionProperty);
        set => SetValue(SortButtonDescriptionProperty, value);
    }

    public string ResultCountText
    {
        get => (string)GetValue(ResultCountTextProperty);
        set => SetValue(ResultCountTextProperty, value);
    }

    public string VerifiedOnlySuffixText
    {
        get => (string)GetValue(VerifiedOnlySuffixTextProperty);
        set => SetValue(VerifiedOnlySuffixTextProperty, value);
    }

    public ICommand? OpenFiltersCommand
    {
        get => (ICommand?)GetValue(OpenFiltersCommandProperty);
        set => SetValue(OpenFiltersCommandProperty, value);
    }

    public ICommand? OpenSortCommand
    {
        get => (ICommand?)GetValue(OpenSortCommandProperty);
        set => SetValue(OpenSortCommandProperty, value);
    }
}
