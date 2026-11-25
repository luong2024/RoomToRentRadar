using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingLocationPickerViewModelTests
{
    [Test]
    public void Initialize_ExistingLocationUsesValueEqualityAndRemainsUnchangedWhenDraftMoves()
    {
        var original = new GeoCoordinate(21.5855, 105.8075);
        var sameValue = new GeoCoordinate(original.Latitude, original.Longitude);
        var picker = new ListingLocationPickerViewModel();
        picker.Initialize(original);
        picker.SetMapState(ListingMapState.Ready);

        var draftChanges = 0;
        picker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(picker.DraftLocation))
            {
                draftChanges++;
            }
        };

        picker.Select(sameValue);
        var moved = original with { Latitude = 21.625 };
        picker.Select(moved);

        Assert.Multiple(() =>
        {
            Assert.That(sameValue, Is.EqualTo(original));
            Assert.That(sameValue, Is.Not.SameAs(original));
            Assert.That(original, Is.EqualTo(new GeoCoordinate(21.5855, 105.8075)));
            Assert.That(picker.DraftLocation, Is.EqualTo(moved));
            Assert.That(draftChanges, Is.EqualTo(1));
        });
    }

    [TestCase(double.NaN, 105, TestName = "Select_RejectsNaNAndPreservesDraft")]
    [TestCase(21, double.PositiveInfinity, TestName = "Select_RejectsInfinityAndPreservesDraft")]
    [TestCase(90.0001, 105, TestName = "Select_RejectsLatitudeOutsideRangeAndPreservesDraft")]
    [TestCase(21, -180.0001, TestName = "Select_RejectsLongitudeOutsideRangeAndPreservesDraft")]
    public void Select_InvalidCoordinatePreservesDraft(double latitude, double longitude)
    {
        var previous = new GeoCoordinate(21, 105);
        var picker = ReadyPicker(previous);

        picker.Select(new GeoCoordinate(latitude, longitude));

        Assert.That(picker.DraftLocation, Is.EqualTo(previous));
    }

    [TestCase(0, 0, TestName = "Select_AcceptsZeroPair")]
    [TestCase(-90, 180, TestName = "Select_AcceptsSouthLatitudeAndEastLongitudeBoundary")]
    [TestCase(90, -180, TestName = "Select_AcceptsNorthLatitudeAndWestLongitudeBoundary")]
    public void Select_ValidCoordinateEnablesConfirmationAndNotifiesSummary(double latitude, double longitude)
    {
        var picker = ReadyPicker();
        var changedProperties = new List<string?>();
        var canExecuteChanged = 0;
        picker.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        picker.ConfirmCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;
        Assert.That(picker.ConfirmCommand.CanExecute(null), Is.False);

        var coordinate = new GeoCoordinate(latitude, longitude);
        picker.Select(coordinate);

        Assert.Multiple(() =>
        {
            Assert.That(picker.DraftLocation, Is.EqualTo(coordinate));
            Assert.That(picker.ConfirmCommand.CanExecute(null), Is.True);
            Assert.That(changedProperties, Does.Contain(nameof(picker.SelectionSummary)));
            Assert.That(canExecuteChanged, Is.Positive);
        });
    }

    [TestCase((int)ListingMapState.Loading, TestName = "Confirm_DisabledUntilMapRenders")]
    [TestCase((int)ListingMapState.Offline, TestName = "Confirm_DisabledWhileMapIsOffline")]
    [TestCase((int)ListingMapState.MissingKey, TestName = "Confirm_DisabledWhenGoogleMapsKeyIsMissing")]
    [TestCase((int)ListingMapState.Unavailable, TestName = "Confirm_DisabledWhenPlatformDoesNotSupportSelection")]
    public async Task Confirm_UnavailableMapCannotReturnDraft(int state)
    {
        var original = new GeoCoordinate(21, 105);
        var picker = ReadyPicker(original);
        var closeCount = 0;
        picker.ClosePickerAsync = _ =>
        {
            closeCount++;
            return Task.FromResult(true);
        };
        picker.SetMapState((ListingMapState)state);

        picker.Select(new GeoCoordinate(22, 106));
        await picker.ConfirmCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(picker.DraftLocation, Is.EqualTo(original));
            Assert.That(picker.ConfirmCommand.CanExecute(null), Is.False);
            Assert.That(picker.CancelCommand.CanExecute(null), Is.True);
            Assert.That(closeCount, Is.Zero);
        });
    }

    [Test]
    public async Task ConfirmAndCancel_CompleteOneSnapshotAndIgnoreLateTaps()
    {
        var picked = new GeoCoordinate(21.5855, 105.8075);
        var picker = ReadyPicker(picked);
        var closeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedResults = new List<ListingLocationPickerResult>();
        picker.ClosePickerAsync = result =>
        {
            requestedResults.Add(result);
            return closeGate.Task;
        };

        var confirm = picker.ConfirmCommand.ExecuteAsync(null);
        await picker.CancelCommand.ExecuteAsync(null);
        picker.Select(new GeoCoordinate(23, 103));
        closeGate.SetResult(true);
        await confirm;
        picker.Select(new GeoCoordinate(24, 104));
        await picker.ConfirmCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(requestedResults, Is.EqualTo(new[] { new ListingLocationPickerResult(
                ListingLocationPickerOutcome.Confirmed, picked) }));
            Assert.That(picker.DraftLocation, Is.EqualTo(picked));
            Assert.That(picker.ConfirmCommand.CanExecute(null), Is.False);
            Assert.That(picker.CancelCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task CloseFailure_AllowsRetryWithoutLosingDraft()
    {
        var picked = new GeoCoordinate(21, 105);
        var picker = ReadyPicker(picked);
        ListingLocationPickerResult? result = null;
        picker.ClosePickerAsync = _ => Task.FromResult(false);

        await picker.ConfirmCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(picker.HasCloseError, Is.True);
            Assert.That(picker.DraftLocation, Is.EqualTo(picked));
            Assert.That(picker.ConfirmCommand.CanExecute(null), Is.True);
        });

        picker.ClosePickerAsync = value =>
        {
            result = value;
            return Task.FromResult(true);
        };
        await picker.CancelCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new ListingLocationPickerResult(ListingLocationPickerOutcome.Cancelled)));
            Assert.That(picker.HasCloseError, Is.False);
        });
    }

    private static ListingLocationPickerViewModel ReadyPicker(GeoCoordinate? initial = null)
    {
        var picker = new ListingLocationPickerViewModel();
        picker.Initialize(initial);
        picker.SetMapState(ListingMapState.Ready);
        return picker;
    }
}
