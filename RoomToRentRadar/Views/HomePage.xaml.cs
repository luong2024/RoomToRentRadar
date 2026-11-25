using System.ComponentModel;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        FilterBottomSheet.StateChanged += OnFilterBottomSheetStateChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.AttachAuthSession();
        _viewModel.RefreshComparisonState();

        if (!_viewModel.HasLoaded)
            _viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DetachAuthSession();
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.IsFilterPanelOpen))
        {
            if (_viewModel.IsFilterPanelOpen && !FilterBottomSheet.IsOpen)
                FilterBottomSheet.Show();
            else if (!_viewModel.IsFilterPanelOpen && FilterBottomSheet.IsOpen)
                FilterBottomSheet.Close();
        }
    }

    private void OnFilterBottomSheetStateChanged(object? sender, Syncfusion.Maui.Toolkit.BottomSheet.StateChangedEventArgs e)
    {
        if (!FilterBottomSheet.IsOpen && _viewModel.IsFilterPanelOpen)
            _viewModel.IsFilterPanelOpen = false;
    }

    private const double ExpandedBrandHeight = 49;
    private const double ScrollCollapseDistance = 120;
    private const double FilterSlotHalfWidth = 29; // Half of (48px Filter Orb + 10px gap) to center Search Capsule on GPU.

    private double _headerCollapseOffset;

    private void OnListingsScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.VerticalOffset <= 2)
        {
            _headerCollapseOffset = 0;
        }
        else if (Math.Abs(e.VerticalDelta) > 0.01)
        {
            // Delta-driven: scrolling down hides gradually; pulling down at ANY scroll position reveals gradually.
            _headerCollapseOffset = Math.Clamp(
                _headerCollapseOffset + e.VerticalDelta,
                0.0,
                ScrollCollapseDistance);
        }

        var progress = _headerCollapseOffset / ScrollCollapseDistance;
        ApplyScrollCollapseProgress(progress);
    }

    private void ApplyScrollCollapseProgress(double progress)
    {
        // 100% GPU compositor transforms (zero CollectionView relayout while scrolling).
        var ease = Easing.SinInOut.Ease(progress);

        // 1. Glide the header smoothly up to tuck the Brand Row out of sight.
        StickyHeaderContainer.TranslationY = -ExpandedBrandHeight * ease;
        HeaderGlassScrim.Opacity = Math.Clamp(1.0 - progress * 1.15, 0.0, 1.0);

        // 2. Brand Row dissolves gently as it glides up.
        var brandOpacity = Math.Clamp(1.0 - progress * 1.18, 0.0, 1.0);
        BrandUtilityRow.Opacity = brandOpacity;
        BrandUtilityRow.Scale = 1.0 - 0.06 * ease;
        BrandUtilityRow.InputTransparent = brandOpacity < 0.2;

        // 3. Filter Orb glides right & dissolves smoothly while Search Capsule glides to exact center.
        var filterOpacity = Math.Clamp(1.0 - progress * 1.25, 0.0, 1.0);
        HomeFilterOrb.Opacity = filterOpacity;
        HomeFilterOrb.Scale = 1.0 - 0.24 * ease;
        HomeFilterOrb.TranslationX = 16.0 * ease;
        HomeFilterOrb.InputTransparent = filterOpacity < 0.2;

        HomeSearchCapsule.TranslationX = FilterSlotHalfWidth * ease;

        // 4. Category Chips glide slightly up & dissolve cleanly, freeing full viewport for room browsing.
        var chipsOpacity = Math.Clamp(1.0 - progress * 1.25, 0.0, 1.0);
        CategoryChipsScroll.Opacity = chipsOpacity;
        CategoryChipsScroll.TranslationY = -14.0 * ease;
        CategoryChipsScroll.Scale = 1.0 - 0.05 * ease;
        CategoryChipsScroll.InputTransparent = chipsOpacity < 0.2;
        var chipsShouldBeVisible = chipsOpacity > 0.02;
        if (CategoryChipsScroll.IsVisible != chipsShouldBeVisible)
            CategoryChipsScroll.IsVisible = chipsShouldBeVisible;
    }

    private void OnThemeToggleTapped(object? sender, TappedEventArgs e)
    {
        MotionEffects.TryLightHaptic();
        App.ToggleTheme();
    }

    private async void OnChipTapped(object? sender, TappedEventArgs e)
    {
        MotionEffects.TryLightHaptic();
        if (sender is VisualElement element)
            await MotionEffects.LiquidPressAsync(element);
    }
}
