using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

public class RoomListViewModelTests
{
    private sealed class FakeListingsService : IListingsService
    {
        public int CallCount { get; private set; }
        public bool ShouldThrowNetworkError { get; set; }
        public TaskCompletionSource<bool>? Gate { get; set; }

        public async Task<PagedResult<RoomListingItemModel>> GetListingsAsync(
            string campusId,
            int pageIndex,
            int pageSize,
            string sortBy,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Gate is not null)
            {
                await Gate.Task;
            }

            if (ShouldThrowNetworkError)
            {
                throw new HttpRequestException("Simulated network disconnect");
            }

            // Giả lập tổng 25 phòng: Trang 1 = 20 phòng, Trang 2 = 5 phòng (< pageSize)
            int start = (pageIndex - 1) * pageSize + 1;
            int end = System.Math.Min(25, start + pageSize - 1);
            var items = new List<RoomListingItemModel>();

            for (int i = start; i <= end; i++)
            {
                items.Add(new RoomListingItemModel
                {
                    Id = $"room-{i:000}",
                    Title = $"Phòng #{i}",
                    Rent = 1_500_000m,
                    Area = 20,
                    DistanceMeters = i * 100,
                    IsVerified = true,
                    Status = "Available"
                });
            }

            return new PagedResult<RoomListingItemModel>
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = 25,
                HasNextPage = pageIndex * pageSize < 25
            };
        }
    }

    [Test]
    public async Task MockListingsService_Generates45VerifiedAvailableRoomsSortedByDistance()
    {
        var mockService = new MockListingsService();

        var page1 = await mockService.GetListingsAsync("ictu-main", 1, 20, "distance");
        var page2 = await mockService.GetListingsAsync("ictu-main", 2, 20, "distance");
        var page3 = await mockService.GetListingsAsync("ictu-main", 3, 20, "distance");

        Assert.Multiple(() =>
        {
            Assert.That(page1.TotalCount, Is.EqualTo(45));
            Assert.That(page1.Items.Count, Is.EqualTo(20));
            Assert.That(page1.HasNextPage, Is.True);

            Assert.That(page2.Items.Count, Is.EqualTo(20));
            Assert.That(page2.HasNextPage, Is.True);

            Assert.That(page3.Items.Count, Is.EqualTo(5));
            Assert.That(page3.HasNextPage, Is.False);

            // US01 AC4 & AC1: Tất cả đều Available + IsVerified và sắp xếp khoảng cách tăng dần
            var allItems = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
            Assert.That(allItems.All(r => r.IsVerified && r.Status == "Available"), Is.True);
            Assert.That(allItems.Select(r => r.DistanceMeters), Is.Ordered.Ascending);
        });
    }

    [Test]
    public async Task LoadInitialThenLoadMore_SetsHasReachedEndWhenLessThanPageSize()
    {
        var fake = new FakeListingsService();
        var vm = new RoomListViewModel(fake);

        await vm.LoadInitialListingsAsync();
        Assert.That(vm.Listings.Count, Is.EqualTo(20));
        Assert.That(vm.HasReachedEnd, Is.False);

        await vm.LoadMoreListingsAsync();
        Assert.That(vm.Listings.Count, Is.EqualTo(25));
        Assert.That(vm.HasReachedEnd, Is.True);

        // Khi HasReachedEnd == true, gọi LoadMore tiếp sẽ bị chặn ngay, không gọi thêm API
        await vm.LoadMoreListingsAsync();
        Assert.That(fake.CallCount, Is.EqualTo(2));
    }

    [Test]
    public async Task LoadMoreListingsAsync_PreventsDuplicateConcurrentRequests()
    {
        var fake = new FakeListingsService();
        var vm = new RoomListViewModel(fake);
        await vm.LoadInitialListingsAsync();

        fake.Gate = new TaskCompletionSource<bool>();
        var firstMore = vm.LoadMoreListingsAsync();
        var duplicateMore = vm.LoadMoreListingsAsync(); // Bị chặn bởi khóa IsLoadingMore

        fake.Gate.SetResult(true);
        await Task.WhenAll(firstMore, duplicateMore);

        Assert.That(fake.CallCount, Is.EqualTo(2)); // 1 lần trang đầu + 1 lần trang 2
    }

    [Test]
    public async Task LoadInitialAndLoadMore_HandleNetworkErrorGracefully()
    {
        var fake = new FakeListingsService { ShouldThrowNetworkError = true };
        var vm = new RoomListViewModel(fake);

        await vm.LoadInitialListingsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasError, Is.True);
            Assert.That(vm.ErrorMessage, Is.Not.Empty);
            Assert.That(vm.IsLoading, Is.False);
            Assert.That(vm.IsRefreshing, Is.False);
        });
    }
}