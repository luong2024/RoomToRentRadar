using System.ComponentModel;
using RoomToRentRadar.Features.Map.ViewModels;

namespace RoomToRentRadar.Features.Map.Views;

public partial class MapSearchPage
{
    private readonly Dictionary<VisualElement, int> _motionVersions = new();
    private bool _motionActive;
    private bool _motionRefreshQueued;
    private bool _previewWasShown;
    private bool _savedWasShown;
    private bool _resultsWereShown;

    private void StartMotion()
    {
        if (_motionActive)
            return;

        _motionActive = true;
        _previewWasShown = false;
        _savedWasShown = false;
        _resultsWereShown = false;
        _model.PropertyChanged += OnMotionPropertyChanged;
        _ = RevealPanelAsync(HeaderPanel, -6);
        QueueMotionRefresh();
    }

    private void StopMotion()
    {
        _motionActive = false;
        _motionRefreshQueued = false;
        _model.PropertyChanged -= OnMotionPropertyChanged;
        foreach (var panel in new VisualElement[] { HeaderPanel, PreviewPanel, SavedPanel, ResultsPanel })
            ResetPanelMotion(panel);
    }

    private void OnMotionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapSearchPageModel.ShowPreviewCard)
            or nameof(MapSearchPageModel.IsSavedPanelOpen)
            or nameof(MapSearchPageModel.HasResults))
            QueueMotionRefresh();
    }

    private void QueueMotionRefresh()
    {
        if (!_motionActive || _motionRefreshQueued)
            return;

        _motionRefreshQueued = true;
        // Allow compiled bindings to apply visibility before animating a panel.
        Dispatcher.Dispatch(() =>
        {
            _motionRefreshQueued = false;
            if (!_motionActive)
                return;

            UpdatePanelMotion(PreviewPanel, _model.ShowPreviewCard, ref _previewWasShown);
            UpdatePanelMotion(SavedPanel, _model.IsSavedPanelOpen, ref _savedWasShown);
            UpdatePanelMotion(ResultsPanel,
                _model.HasResults && !_model.ShowPreviewCard && !_model.IsSavedPanelOpen,
                ref _resultsWereShown);
        });
    }

    private void UpdatePanelMotion(VisualElement panel, bool shown, ref bool wasShown)
    {
        if (shown == wasShown)
            return;

        wasShown = shown;
        if (shown)
            _ = RevealPanelAsync(panel, 10);
        else
            ResetPanelMotion(panel);
    }

    private void ResetPanelMotion(VisualElement panel)
    {
        _motionVersions[panel] = _motionVersions.GetValueOrDefault(panel) + 1;
        panel.CancelAnimations();
        panel.Opacity = 1;
        panel.TranslationX = 0;
        panel.TranslationY = 0;
    }

    private async Task RevealPanelAsync(VisualElement panel, double offset)
    {
        ResetPanelMotion(panel);
        if (!_motionActive || !panel.IsVisible || !MotionIsEnabled())
            return;

        var version = _motionVersions[panel];
        panel.Opacity = 0.82;
        panel.TranslationY = offset;
        try
        {
            await Task.WhenAll(
                panel.FadeToAsync(1, 180, Easing.CubicOut),
                panel.TranslateToAsync(0, 0, 180, Easing.CubicOut));
        }
        catch (OperationCanceledException)
        {
            // Closing, reopening or leaving the page supersedes the transition.
        }
        finally
        {
            if (_motionVersions.GetValueOrDefault(panel) == version)
            {
                panel.Opacity = 1;
                panel.TranslationY = 0;
            }
        }
    }

    private static bool MotionIsEnabled()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#endif
        return true;
    }
}
