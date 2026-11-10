using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HomeViewModelTests
{
    [Test]
    public async Task LoadAsync_WhenApiReturnsListings_PopulatesListingsAndClearsBusyState()
    {
        var listing = new ListingSummaryDto(
            Guid.NewGuid(),
            "Sunny room",
            1_500_000,
            25,
            1,
            "1 Main St",
            16.0,
            108.0,
            DateTime.UtcNow,
            null);
        var apiClient = new FakeApiClient(new PagedResult<ListingSummaryDto>(
            new[] { listing },
            new PaginationInfo(5, null, false)));
        var viewModel = new HomeViewModel(apiClient, new FakeErrorHandler());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.That(viewModel.Listings, Has.Count.EqualTo(1));
        Assert.That(viewModel.StatusMessage, Is.EqualTo("1 latest listings"));
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(apiClient.RequestedLimit, Is.EqualTo(5));
    }

    [Test]
    public async Task LoadAsync_WhenApiThrows_ReportsErrorAndClearsBusyState()
    {
        var errorHandler = new FakeErrorHandler();
        var viewModel = new HomeViewModel(
            new FakeApiClient(new InvalidOperationException("boom")),
            errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.That(viewModel.Listings, Is.Empty);
        Assert.That(viewModel.StatusMessage, Is.EqualTo("Could not load listings."));
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(errorHandler.HandledException, Is.TypeOf<InvalidOperationException>());
    }

    private sealed class FakeApiClient : IApiClient
    {
        private readonly PagedResult<ListingSummaryDto>? _result;
        private readonly Exception? _exception;

        public FakeApiClient(PagedResult<ListingSummaryDto> result)
        {
            _result = result;
        }

        public FakeApiClient(Exception exception)
        {
            _exception = exception;
        }

        public int? RequestedLimit { get; private set; }

        public Task<PagedResult<ListingSummaryDto>> GetListingsAsync(int limit, CancellationToken cancellationToken = default)
        {
            RequestedLimit = limit;

            return _exception is null
                ? Task.FromResult(_result!)
                : Task.FromException<PagedResult<ListingSummaryDto>>(_exception);
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public Exception? HandledException { get; private set; }

        public void HandleError(Exception ex)
        {
            HandledException = ex;
        }
    }
}
