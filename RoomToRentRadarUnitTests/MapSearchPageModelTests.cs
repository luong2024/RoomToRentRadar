using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Map.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MapSearchPageModelTests
{
    private static readonly GeoPoint A = new(10.0, 106.0);
    private static readonly GeoPoint B = new(10.0, 107.0);
    private static readonly GeoPoint C = new(11.0, 107.0);

    [Test]
    public void AddVertex_IsIgnoredUntilDrawingStarts()
    {
        var model = CreateModel(out _, out _, out _);

        model.AddVertex(A.Lat, A.Lng);
        Assert.That(model.Draft.Vertices, Is.Empty);

        model.StartDrawingCommand.Execute(null);
        model.AddVertex(A.Lat, A.Lng);

        Assert.That(model.Draft.Vertices, Is.EqualTo(new[] { A }));
        Assert.That(model.CanFinishDrawing, Is.False);
    }

    [Test]
    public async Task FinishDrawing_WithTooFewVertices_KeepsDrawing()
    {
        var model = CreateModel(out _, out _, out _);
        model.StartDrawingCommand.Execute(null);
        model.AddVertex(A.Lat, A.Lng);
        model.AddVertex(B.Lat, B.Lng);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.IsDrawing, Is.True);
        Assert.That(model.Draft.IsClosed, Is.False);
        Assert.That(model.StatusMessage, Does.Contain("3 đỉnh"));
    }

    [Test]
    public async Task FinishDrawing_ClosesDraftAndSendsTheAreaSearch()
    {
        var listing = Listing("Phòng gần", 10.3, 106.6);
        var model = CreateModel(out var api, out _, out _, Result(new[] { listing }, 1));
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.IsDrawing, Is.False);
        Assert.That(model.HasPolygon, Is.True);
        Assert.That(api.LastRequest, Is.Not.Null);
        Assert.That(api.LastRequest!.Area, Is.Not.Null);
        Assert.That(api.LastRequest.Area!.Type, Is.EqualTo("Polygon"));
        var ring = api.LastRequest.Area.Coordinates![0];
        Assert.That(ring, Has.Length.EqualTo(4));
        Assert.That(ring[0], Is.EqualTo(new[] { 106.0, 10.0 }));
        Assert.That(ring[^1], Is.EqualTo(ring[0]));
        Assert.That(api.LastRequest.Sort, Is.EqualTo("distance_asc"));
        Assert.That(api.LastRequest.Limit, Is.EqualTo(MapSearchPageModel.ResultLimit));
        Assert.That(model.Listings, Has.Count.EqualTo(1));
        Assert.That(model.ResultsText, Is.EqualTo("Hiển thị 1 / 1 phòng"));
    }

    [Test]
    public async Task FinishDrawing_ComputesDistancesFromTheDraftCentroid()
    {
        var near = Listing("Gần", 10.3, 106.6);
        var far = Listing("Xa", 12, 108);
        var model = CreateModel(out _, out _, out _, Result(near, far));
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        var centroid = new GeoPoint(10d + 1d / 3d, 106d + 2d / 3d);
        var expectedNear = RoomListingService.DistanceKm(centroid.Lat, centroid.Lng, near.Lat, near.Lng);
        var expectedFar = RoomListingService.DistanceKm(centroid.Lat, centroid.Lng, far.Lat, far.Lng);

        Assert.That(model.Listings[0].DistanceKm, Is.EqualTo(expectedNear).Within(1e-9));
        Assert.That(model.Listings[1].DistanceKm, Is.EqualTo(expectedFar).Within(1e-9));
        Assert.That(model.Listings[0].DistanceKm, Is.LessThan(model.Listings[1].DistanceKm));
        Assert.That(model.Listings.Select(item => item.Listing), Is.EqualTo(new[] { near, far }));
    }

    [Test]
    public async Task ChangingSort_ReSearchesWithTheSelectedSort()
    {
        var model = CreateModel(out var api, out _, out _);
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);
        api.Requests.Clear();

        model.SelectedSortIndex = 0;
        await (model.SearchCommand.ExecutionTask ?? Task.CompletedTask);

        Assert.That(api.Requests, Has.Count.EqualTo(1));
        Assert.That(api.Requests[0].Sort, Is.EqualTo("price_asc"));
    }

    [Test]
    public async Task SaveArea_PersistsANamedAreaWithTheDrawnVertices()
    {
        var model = CreateModel(out _, out var store, out _);
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);

        model.NewAreaName = "  Quận 10  ";
        model.SaveAreaCommand.Execute(null);

        Assert.That(store.Stored, Has.Count.EqualTo(1));
        Assert.That(store.Stored[0].Name, Is.EqualTo("Quận 10"));
        Assert.That(store.Stored[0].Vertices, Is.EqualTo(new[] { A, B, C }));
        Assert.That(model.SavedAreas, Has.Count.EqualTo(1));
        Assert.That(model.HasSavedAreas, Is.True);
        Assert.That(model.NewAreaName, Is.Empty);
        Assert.That(model.SelectedSavedArea, Is.SameAs(store.Stored[0]));
    }

    [Test]
    public async Task ApplySavedArea_LoadsTheVerticesAndSearches()
    {
        var saved = new SavedAreaSearch("area-1", "Quận 1", [A, B, C], DateTime.UtcNow);
        var model = CreateModel(out var api, out _, out _, Result(Listing("Phòng", 10.2, 106.2)), storeAreas: [saved]);
        model.SelectedSavedArea = model.SavedAreas.Single();

        model.ApplySavedAreaCommand.Execute(null);
        await (model.SearchCommand.ExecutionTask ?? Task.CompletedTask);

        Assert.That(model.Draft.IsClosed, Is.True);
        Assert.That(model.Draft.Vertices, Is.EqualTo(new[] { A, B, C }));
        Assert.That(api.LastRequest!.Area!.Coordinates![0], Has.Length.EqualTo(4));
        Assert.That(model.Listings, Has.Count.EqualTo(1));
        Assert.That(model.StatusMessage, Does.Contain("Tìm thấy 1 phòng"));
    }

    [Test]
    public void DeleteSavedArea_RemovesItFromTheStore()
    {
        var saved = new SavedAreaSearch("area-1", "Quận 1", [A, B, C], DateTime.UtcNow);
        var model = CreateModel(out _, out var store, out _, storeAreas: [saved]);
        model.SelectedSavedArea = model.SavedAreas.Single();

        model.DeleteSavedAreaCommand.Execute(null);

        Assert.That(model.SavedAreas, Is.Empty);
        Assert.That(store.Stored, Is.Empty);
        Assert.That(model.HasSavedAreas, Is.False);
    }

    [Test]
    public async Task SelfIntersectingPolygon_ShowsTheSelfIntersectionMessage()
    {
        var model = CreateModel(
            out _,
            out _,
            out _,
            exception: new ApiException(HttpStatusCode.UnprocessableEntity, "self intersecting", "self_intersecting_area"));
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.Listings, Is.Empty);
        Assert.That(model.StatusMessage, Does.Contain("tự cắt"));
        Assert.That(model.IsBusy, Is.False);
    }

    [Test]
    public async Task AreaTooLarge_ShowsTheAreaLimitMessage()
    {
        var model = CreateModel(
            out _,
            out _,
            out _,
            exception: new ApiException(HttpStatusCode.UnprocessableEntity, "too large", "area_too_large"));
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.Listings, Is.Empty);
        Assert.That(model.StatusMessage, Does.Contain("100 km²"));
        Assert.That(model.IsBusy, Is.False);
    }

    [Test]
    public async Task TooManyVertices_ShowsTheVertexLimitMessage()
    {
        var model = CreateModel(
            out _,
            out _,
            out _,
            exception: new ApiException(HttpStatusCode.UnprocessableEntity, "too many", "invalid_area_vertex_count"));
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.StatusMessage, Does.Contain("200"));
    }

    [Test]
    public async Task ShowPreviewCard_IsHiddenWhileTheSavedPanelIsOpen()
    {
        var model = CreateModel(out _, out _, out _, Result(Listing("Phòng", 10.2, 106.2)));
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);
        model.SelectListing(model.Listings[0]);
        Assert.That(model.ShowPreviewCard, Is.True);

        model.ToggleSavedAreasCommand.Execute(null);

        Assert.That(model.HasSelection, Is.True);
        Assert.That(model.ShowPreviewCard, Is.False);

        model.ToggleSavedAreasCommand.Execute(null);

        Assert.That(model.ShowPreviewCard, Is.True);
    }

    [Test]
    public async Task ChangingSortDuringSearch_AppliesOnlyTheLatestResult()
    {
        var first = Listing("Cũ", 10.1, 106.1);
        var newest = Listing("Mới", 10.2, 106.2);
        var api = new QueuedApiClient();
        var model = new MapSearchPageModel(api, new FakeSavedAreaStore([]), new FakeErrorHandler());
        DrawTriangle(model);

        model.FinishDrawingCommand.Execute(null);
        Assert.That(api.Requests, Has.Count.EqualTo(1));

        model.SelectedSortIndex = 0;
        Assert.That(api.Requests, Has.Count.EqualTo(2));
        Assert.That(api.CancellationTokens[0].IsCancellationRequested, Is.True);
        Assert.That(api.Requests[0].Sort, Is.EqualTo(ListingSortWire.DistanceAsc));
        Assert.That(api.Requests[1].Sort, Is.EqualTo(ListingSortWire.PriceAsc));

        var finishExecution = model.FinishDrawingCommand.ExecutionTask;

        api.Complete(1, Result(newest));
        await (model.SearchCommand.ExecutionTask ?? Task.CompletedTask);

        Assert.That(model.Listings.Select(item => item.Listing), Is.EqualTo(new[] { newest }));
        Assert.That(model.StatusMessage, Does.Contain("Tìm thấy 1 phòng"));
        Assert.That(model.IsBusy, Is.False);

        api.Complete(0, Result(first));
        await (finishExecution ?? Task.CompletedTask);

        Assert.That(model.Listings.Select(item => item.Listing), Is.EqualTo(new[] { newest }));
        Assert.That(model.IsBusy, Is.False);
    }

    [Test]
    public async Task ApiFailure_IsReportedThroughTheErrorHandler()
    {
        var exception = new ApiException(HttpStatusCode.InternalServerError, "boom");
        var model = CreateModel(out _, out _, out var errors, exception: exception);
        DrawTriangle(model);

        await model.FinishDrawingCommand.ExecuteAsync(null);

        Assert.That(model.StatusMessage, Does.Contain("Không thể tìm phòng"));
        Assert.That(errors.HandledException, Is.SameAs(exception));
        Assert.That(model.IsBusy, Is.False);
    }

    [Test]
    public async Task StartDrawing_ClearsPreviousResultsAndDraft()
    {
        var model = CreateModel(out _, out _, out _, Result(Listing("Phòng", 10.2, 106.2)));
        DrawTriangle(model);
        await model.FinishDrawingCommand.ExecuteAsync(null);
        model.SelectListing(model.Listings[0]);
        Assert.That(model.HasResults, Is.True);

        model.StartDrawingCommand.Execute(null);

        Assert.That(model.Listings, Is.Empty);
        Assert.That(model.HasResults, Is.False);
        Assert.That(model.HasSelection, Is.False);
        Assert.That(model.Draft.Vertices, Is.Empty);
        Assert.That(model.Draft.IsClosed, Is.False);
        Assert.That(model.IsDrawing, Is.True);
    }

    [Test]
    public void SelectedSortDefaultIsDistanceAscending()
    {
        var model = CreateModel(out _, out _, out _);

        Assert.That(model.SortOptions[model.SelectedSortIndex].WireValue, Is.EqualTo(ListingSortWire.DistanceAsc));
    }

    private static void DrawTriangle(MapSearchPageModel model)
    {
        model.StartDrawingCommand.Execute(null);
        model.AddVertex(A.Lat, A.Lng);
        model.AddVertex(B.Lat, B.Lng);
        model.AddVertex(C.Lat, C.Lng);
    }

    private static ListingSummaryDto Listing(string title, double lat, double lng)
        => new(Guid.NewGuid(), title, 3_000_000, 25, 1, "1 Đường ABC", lat, lng, DateTime.UtcNow, null);

    private static SearchResultDto<ListingSummaryDto> Result(params ListingSummaryDto[] data)
        => Result(data, data.Length);

    private static SearchResultDto<ListingSummaryDto> Result(IReadOnlyList<ListingSummaryDto> data, int totalCount)
        => new(data, new PaginationInfo(MapSearchPageModel.ResultLimit, null, false), totalCount, false);

    private static MapSearchPageModel CreateModel(
        out FakeApiClient apiClient,
        out FakeSavedAreaStore store,
        out FakeErrorHandler errors,
        SearchResultDto<ListingSummaryDto>? result = null,
        Exception? exception = null,
        IReadOnlyList<SavedAreaSearch>? storeAreas = null)
    {
        apiClient = new FakeApiClient
        {
            Result = result ?? Result([]),
            Exception = exception
        };
        store = new FakeSavedAreaStore(storeAreas ?? []);
        errors = new FakeErrorHandler();
        return new MapSearchPageModel(apiClient, store, errors);
    }

    private sealed class FakeApiClient : IApiClient
    {
        public SearchResultDto<ListingSummaryDto> Result { get; init; } = new([], new PaginationInfo(100, null, false), 0, false);

        public Exception? Exception { get; init; }

        public List<AreaSearchRequest> Requests { get; } = [];

        public AreaSearchRequest? LastRequest => Requests.Count == 0 ? null : Requests[^1];

        public Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<SearchResultDto<ListingSummaryDto>>(Exception);
        }
    }

    private sealed class QueuedApiClient : IApiClient
    {
        private readonly List<TaskCompletionSource<SearchResultDto<ListingSummaryDto>>> _pending = [];

        public List<AreaSearchRequest> Requests { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);
            var completion = new TaskCompletionSource<SearchResultDto<ListingSummaryDto>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(completion);
            return completion.Task;
        }

        public void Complete(int index, SearchResultDto<ListingSummaryDto> result)
            => _pending[index].TrySetResult(result);
    }

    private sealed class FakeSavedAreaStore(IReadOnlyList<SavedAreaSearch> areas) : ISavedAreaSearchStore
    {
        public List<SavedAreaSearch> Stored { get; private set; } = [.. areas];

        public IReadOnlyList<SavedAreaSearch> GetAll() => Stored;

        public void ReplaceAll(IReadOnlyList<SavedAreaSearch> areas) => Stored = [.. areas];
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public Exception? HandledException { get; private set; }

        public void HandleError(Exception ex) => HandledException = ex;
    }
}
