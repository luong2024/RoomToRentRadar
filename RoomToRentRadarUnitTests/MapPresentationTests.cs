namespace RoomToRentRadarUnitTests;

public partial class MapSearchPageModelTests
{
    [Test]
    public void InitialMapState_DoesNotClaimAnEmptySearchOrAllowSearchAgain()
    {
        var model = CreateModel(out _, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(model.ShowEmptyState, Is.False);
            Assert.That(model.ShowSearchError, Is.False);
            Assert.That(model.ShowSearchState, Is.False);
            Assert.That(model.CanSearchAgain, Is.False);
        });
    }

    [Test]
    public async Task SuccessfulEmptySearch_ShowsEmptyStateAndAllowsRetryInTheSamePolygon()
    {
        var model = CreateModel(out _, out _, out _);
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.HasCompletedSearch, Is.True);
            Assert.That(model.ShowEmptyState, Is.True);
            Assert.That(model.ShowSearchState, Is.True);
            Assert.That(model.ShowSearchError, Is.False);
            Assert.That(model.CanSearchAgain, Is.True);
            Assert.That(model.HasPolygon, Is.True);
        });
    }

    [Test]
    public async Task FailedSearch_ShowsErrorInsteadOfEmptyResultsAndNewDraftClearsIt()
    {
        var model = CreateModel(out _, out _, out _, exception: new HttpRequestException("offline"));
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ShowSearchError, Is.True);
            Assert.That(model.ShowEmptyState, Is.False);
            Assert.That(model.ShowSearchState, Is.True);
            Assert.That(model.CanSearchAgain, Is.True);
        });

        model.StartDrawingCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ShowSearchError, Is.False);
            Assert.That(model.ShowEmptyState, Is.False);
            Assert.That(model.ShowSearchState, Is.False);
            Assert.That(model.CanSearchAgain, Is.False);
        });
    }

    [Test]
    public async Task SearchPresentation_HidesEmptyOverlayDuringBusyStateAndSavedAreas()
    {
        var model = CreateModel(out _, out _, out _);
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);

        model.IsBusy = true;
        Assert.Multiple(() =>
        {
            Assert.That(model.ShowSearchState, Is.False);
            Assert.That(model.CanSearchAgain, Is.False);
        });
        model.IsBusy = false;
        model.ToggleSavedAreasCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ShowSearchState, Is.False);
            Assert.That(model.ShowEmptyState, Is.True);
        });
    }
}
