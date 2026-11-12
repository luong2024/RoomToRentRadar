using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Map.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MapSearchCompletionTests
{
    private static readonly GeoPoint[] Vertices =
    [
        new(10.77, 106.69), new(10.77, 106.70), new(10.775, 106.705),
        new(10.78, 106.70), new(10.78, 106.69), new(10.775, 106.685)
    ];

    [Test]
    public async Task DrawingSixVertices_KeepsEveryTapAndRequestsRedraw_OnlySearchesOnFinish()
    {
        var api = new QueueApiClient(Page(null));
        var model = Create(api);
        model.StartDrawingCommand.Execute(null);
        var redraws = 0;
        model.MapDataChanged += (_, _) => redraws++;

        for (var index = 0; index < Vertices.Length; index++)
        {
            model.AddVertex(Vertices[index].Lat, Vertices[index].Lng);
            Assert.Multiple(() =>
            {
                Assert.That(model.Draft.Vertices, Has.Count.EqualTo(index + 1));
                Assert.That(model.Draft.IsClosed, Is.False);
                Assert.That(model.IsDrawing, Is.True);
                Assert.That(api.Requests, Is.Empty);
            });
        }

        Assert.That(redraws, Is.EqualTo(Vertices.Length));
        Assert.That(model.CanFinishDrawing, Is.True);
        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.Draft.Vertices, Is.EqualTo(Vertices));
            Assert.That(model.Draft.IsClosed, Is.True);
            Assert.That(api.Requests, Has.Count.EqualTo(1));
            Assert.That(api.Requests[0].Area!.Coordinates![0], Has.Length.EqualTo(7));
            Assert.That(api.Requests[0].Area!.Coordinates![0][0], Is.EqualTo(new[] { Vertices[0].Lng, Vertices[0].Lat }));
            Assert.That(api.Requests[0].Area!.Coordinates![0][^1], Is.EqualTo(api.Requests[0].Area!.Coordinates![0][0]));
        });
    }

    [Test]
    public async Task CancelClosedArea_ClearsGeometryPinsPreviewAndPaging()
    {
        var model = Create(new QueueApiClient(Page("next", Listing("Known"))));
        await DrawAndSearch(model);
        model.SelectListing(model.Listings[0]);
        Assert.That(model.CanLoadMore, Is.True);

        model.CancelDrawingCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.Draft.Vertices, Is.Empty);
            Assert.That(model.Draft.IsClosed, Is.False);
            Assert.That(model.IsDrawing, Is.False);
            Assert.That(model.Listings, Is.Empty);
            Assert.That(model.SelectedListing, Is.Null);
            Assert.That(model.ResultsText, Is.Empty);
            Assert.That(model.CanLoadMore, Is.False);
            Assert.That(model.ShowSearchButton, Is.False);
        });
    }

    [Test]
    public async Task CancelArea_WhileSearchPending_IgnoresLateDatabaseResponse()
    {
        var pending = new TaskCompletionSource<SearchResultDto<ListingSummaryDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new QueueApiClient(pending.Task);
        var model = Create(api);
        Draw(model);
        var searching = model.FinishDrawingCommand.ExecuteAsync(null);

        model.CancelDrawingCommand.Execute(null);
        pending.SetResult(Page("stale-next", Listing("Stale")));
        await searching;

        Assert.Multiple(() =>
        {
            Assert.That(api.Tokens[0].IsCancellationRequested, Is.True);
            Assert.That(model.Listings, Is.Empty);
            Assert.That(model.Draft.Vertices, Is.Empty);
            Assert.That(model.CanLoadMore, Is.False);
            Assert.That(model.IsBusy, Is.False);
            Assert.That(model.HasSelection, Is.False);
        });
    }

    [Test]
    public async Task CancelArea_WhileNextPagePending_DoesNotRepopulatePins()
    {
        var pending = new TaskCompletionSource<SearchResultDto<ListingSummaryDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new QueueApiClient(Page("next", Listing("First")), pending.Task);
        var model = Create(api);
        await DrawAndSearch(model);
        var nextPage = model.LoadMoreCommand.ExecuteAsync(null);

        model.CancelDrawingCommand.Execute(null);
        pending.SetResult(Page(null, Listing("Stale page")));
        await nextPage;

        Assert.Multiple(() =>
        {
            Assert.That(api.Tokens[1].IsCancellationRequested, Is.True);
            Assert.That(model.Listings, Is.Empty);
            Assert.That(model.CanLoadMore, Is.False);
            Assert.That(model.IsLoadingMore, Is.False);
        });
    }

    [Test]
    public async Task UndoAndRedraw_SearchesOnlyTheEditedGeometry()
    {
        var api = new QueueApiClient(Page(null));
        var model = Create(api);
        Draw(model);
        model.UndoVertexCommand.Execute(null);
        var replacement = new GeoPoint(10.774, 106.686);
        model.AddVertex(replacement.Lat, replacement.Lng);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.Draft.Vertices, Has.Count.EqualTo(6));
            Assert.That(model.Draft.Vertices[^1], Is.EqualTo(replacement));
            Assert.That(api.Requests[0].Area!.Coordinates![0][^2], Is.EqualTo(new[] { replacement.Lng, replacement.Lat }));
        });
        model.CancelDrawingCommand.Execute(null);
        model.StartDrawingCommand.Execute(null);
        model.AddVertex(10.76, 106.68);
        Assert.That(model.Draft.Vertices, Is.EqualTo(new[] { new GeoPoint(10.76, 106.68) }));
    }

    [Test]
    public async Task FinishSelfIntersectingArea_KeepsDrawingAndDoesNotSearch()
    {
        var api = new QueueApiClient();
        var model = Create(api);
        model.StartDrawingCommand.Execute(null);
        foreach (var vertex in new[]
        {
            new GeoPoint(10.77, 106.69), new GeoPoint(10.78, 106.70),
            new GeoPoint(10.77, 106.70), new GeoPoint(10.78, 106.69)
        })
            model.AddVertex(vertex.Lat, vertex.Lng);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(api.Requests, Is.Empty);
            Assert.That(model.IsDrawing, Is.True);
            Assert.That(model.Draft.IsClosed, Is.False);
            Assert.That(model.Draft.Vertices, Has.Count.EqualTo(4));
            Assert.That(model.CanSaveCurrentArea, Is.False);
        });
    }

    private static MapSearchPageModel Create(QueueApiClient api)
        => new(api, new MemoryStore(), new RecordingErrors());

    private static void Draw(MapSearchPageModel model)
    {
        model.StartDrawingCommand.Execute(null);
        foreach (var vertex in Vertices)
            model.AddVertex(vertex.Lat, vertex.Lng);
    }

    private static async Task DrawAndSearch(MapSearchPageModel model)
    {
        Draw(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);
    }

    private static ListingSummaryDto Listing(string title)
        => new(Guid.NewGuid(), title, 1_000_000, 20, 1, "Test address", 10.772, 106.695, DateTime.UnixEpoch, null);

    private static SearchResultDto<ListingSummaryDto> Page(string? cursor, params ListingSummaryDto[] data)
        => new(data, new PaginationInfo(MapSearchPageModel.ResultLimit, cursor, cursor is not null), data.Length, false);

    private sealed class MemoryStore : ISavedAreaSearchStore
    {
        public IReadOnlyList<SavedAreaSearch> GetAll() => [];
        public void ReplaceAll(IReadOnlyList<SavedAreaSearch> areas) { }
    }

    private sealed class RecordingErrors : IErrorHandler
    {
        public void HandleError(Exception ex) { }
    }

    private sealed class QueueApiClient(params object[] responses) : IApiClient
    {
        private readonly Queue<object> _responses = new(responses);
        public List<AreaSearchRequest> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(AreaSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Tokens.Add(cancellationToken);
            return _responses.Dequeue() switch
            {
                SearchResultDto<ListingSummaryDto> page => Task.FromResult(page),
                Task<SearchResultDto<ListingSummaryDto>> pending => pending,
                Exception error => Task.FromException<SearchResultDto<ListingSummaryDto>>(error),
                _ => throw new InvalidOperationException("Unexpected fake response.")
            };
        }

        public Task<ListingSearchResultDto> GetListingsAsync(int limit, string? cursor = null,
            ActiveListingFiltersDto? filters = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(Guid id, int limit = 4, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ListingContactFetchResult> GetListingContactAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
