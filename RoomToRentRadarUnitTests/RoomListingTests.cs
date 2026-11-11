using System.Diagnostics;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadarUnitTests;

public class RoomListingTests
{
    private static Campus Campus => DemoRoomListings.Campuses[0];

    [Test]
    public async Task FiltersAndSortsBeforePaging()
    {
        var source = DemoRoomListings.Create().ToArray();
        var service = new RoomListingService(source);
        var items = new List<RoomListingItem>();
        bool more;
        do
        {
            var page = await service.GetPageAsync(Campus, items.Count, 20);
            items.AddRange(page.Items);
            more = page.HasMore;
        } while (more);
        Assert.That(items.Count, Is.EqualTo(source.Count(r => r.IsVerified && r.Status == ListingStatus.Available)));
        Assert.That(items.All(r => r.IsVerified && r.Room.Status == ListingStatus.Available), Is.True);
        Assert.That(items.Select(r => r.DistanceKm), Is.Ordered);
        Assert.That(items.Select(r => r.Room.Id).Distinct().Count(), Is.EqualTo(items.Count));
    }

    [Test]
    public async Task CampusPickerSupportsMauiItemsSourceAndTemporaryDeselection()
    {
        var model = new RoomListingPageModel(new RoomListingService(DemoRoomListings.Create()));
        Assert.That(model.Campuses, Is.InstanceOf<System.Collections.IList>());
        model.SelectedCampus = model.Campuses[1];
        model.SelectedCampus = null!;
        Assert.That(model.SelectedCampus, Is.EqualTo(model.Campuses[1]));
        await model.InitializeCommand.ExecuteAsync(null);
        Assert.That(model.HasError, Is.False);
        Assert.That(model.Rooms.Count, Is.EqualTo(20));
    }

    [Test]
    public void DistanceUsesCampusCoordinates()
    {
        Assert.That(RoomListingService.DistanceKm(0, 0, 0, 0), Is.Zero);
        Assert.That(RoomListingService.DistanceKm(0, 0, 0, 1), Is.EqualTo(111.195).Within(.01));
    }

    [Test]
    public async Task EmptySourceHasNoMorePages()
    {
        var page = await new RoomListingService([]).GetPageAsync(Campus, 0, 20);
        Assert.That(page.Items, Is.Empty);
        Assert.That(page.HasMore, Is.False);
    }

    [Test]
    public async Task Processes500ListingsWithinThreeSeconds()
    {
        var timer = Stopwatch.StartNew();
        var service = new RoomListingService(DemoRoomListings.Create());
        var page = await service.GetPageAsync(Campus, 0, 20);
        timer.Stop();
        TestContext.Out.WriteLine($"500 listings, first page: {timer.Elapsed.TotalMilliseconds:F2} ms (service only)");
        Assert.That(page.Items.Count, Is.EqualTo(20));
        Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)));
    }

    [Test]
    public async Task RefreshResetsPagesAndCampusSort()
    {
        var service = new RoomListingService(DemoRoomListings.Create());
        var model = new RoomListingPageModel(service);
        await model.InitializeCommand.ExecuteAsync(null);
        await model.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(model.Rooms.Count, Is.EqualTo(40));
        model.SelectedCampus = DemoRoomListings.Campuses[1];
        await model.RefreshCommand.ExecutionTask!;
        var expected = await service.GetPageAsync(model.SelectedCampus, 0, 20);
        Assert.That(model.Rooms, Is.EqualTo(expected.Items));
    }

    [Test]
    public async Task FailedPageCanBeRetriedWithoutLosingItems()
    {
        var service = new FailingService();
        var model = new RoomListingPageModel(service);
        await model.InitializeCommand.ExecuteAsync(null);
        service.Fail = true;
        await model.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(model.HasError, Is.True);
        Assert.That(model.Rooms.Count, Is.EqualTo(20));
        service.Fail = false;
        await model.RetryCommand.ExecuteAsync(null);
        Assert.That(model.HasError, Is.False);
        Assert.That(model.Rooms.Count, Is.EqualTo(40));
    }

    [Test]
    public async Task CampusChangeIgnoresOlderResponseAndBlocksDuplicateLoad()
    {
        var service = new DeferredService();
        var model = new RoomListingPageModel(service);
        var initial = model.InitializeCommand.ExecuteAsync(null);
        await model.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(service.Requests.Count, Is.EqualTo(1));
        model.SelectedCampus = DemoRoomListings.Campuses[1];
        Assert.That(service.Requests.Count, Is.EqualTo(2));
        var room = DemoRoomListings.Create().First();
        service.Requests[1].SetResult(new ListingPage([new(room, 2)], false));
        await model.RefreshCommand.ExecutionTask!;
        service.Requests[0].SetResult(new ListingPage([new(room, 9)], true));
        await initial;
        Assert.That(model.Rooms.Single().DistanceKm, Is.EqualTo(2));
        Assert.That(model.HasMore, Is.False);
        await model.LoadMoreCommand.ExecuteAsync(null);
        Assert.That(service.Requests.Count, Is.EqualTo(2));
    }

    private class DeferredService : IRoomListingService
    {
        public List<TaskCompletionSource<ListingPage>> Requests { get; } = [];
        public Task<ListingPage> GetPageAsync(Campus campus, int offset, int pageSize, CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<ListingPage>();
            Requests.Add(request);
            return request.Task;
        }
    }

    private class FailingService : IRoomListingService
    {
        public bool Fail { get; set; }
        private readonly RoomListingService _inner = new(DemoRoomListings.Create());
        public Task<ListingPage> GetPageAsync(Campus campus, int offset, int pageSize, CancellationToken cancellationToken = default)
            => Fail ? throw new IOException("Test failure") : _inner.GetPageAsync(campus, offset, pageSize, cancellationToken);
    }
}
