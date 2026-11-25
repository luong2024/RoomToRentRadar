using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Favourites;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class FavouriteServiceTests
{
    private static (FavouriteService Service, IListingRepository ListingRepo) CreateSut(
        IEnumerable<ListingDocument>? customListings = null)
    {
        IListingRepository listingRepo = customListings is null
            ? new InMemoryListingRepository()
            : new InMemoryListingRepository(customListings);
        var favRepo = new InMemoryFavouriteRepository();
        return (new FavouriteService(favRepo, listingRepo), listingRepo);
    }

    private static FavouritesController CreateController(
        IFavouriteService service,
        string? headerUserId = null,
        string? claimUserId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headerUserId is not null)
        {
            httpContext.Request.Headers[FavouritesController.UserIdHeaderName] = headerUserId;
        }

        if (claimUserId is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, claimUserId)], "Bearer"));
        }

        return new FavouritesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public async Task Toggle_AddRemoveAndUndoReAdd_IsIdempotentAndReturnsLiveRentAndStatus()
    {
        var (sut, _) = CreateSut();

        var added = await sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-001",
            Action = "Add"
        });

        added.IsFavourited.Should().BeTrue();
        added.ActionApplied.Should().Be("Added");
        added.TotalFavourites.Should().Be(1);
        added.SyncVersion.Should().Be(1);
        added.CurrentRent.Should().Be(1_800_000m);
        added.CurrentStatus.Should().Be("Available");
        added.Item.Should().NotBeNull();
        added.Item!.SavedRent.Should().Be(1_800_000m);

        // Gọi Add lặp lại -> idempotent NoChange, không nhân bản item hay tăng SyncVersion
        var duplicateAdd = await sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-001",
            Action = "add"
        });
        duplicateAdd.IsFavourited.Should().BeTrue();
        duplicateAdd.ActionApplied.Should().Be("NoChange");
        duplicateAdd.TotalFavourites.Should().Be(1);
        duplicateAdd.SyncVersion.Should().Be(1);

        // Xóa khỏi yêu thích
        var removed = await sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-001",
            Action = "Remove"
        });
        removed.IsFavourited.Should().BeFalse();
        removed.ActionApplied.Should().Be("Removed");
        removed.TotalFavourites.Should().Be(0);
        removed.SyncVersion.Should().Be(2);
        removed.CurrentRent.Should().Be(1_800_000m);
        removed.CurrentStatus.Should().Be("Available");
        removed.Item.Should().BeNull();

        // Hoàn tác (Undo trong 5s ở T06.2) -> Add lại thành công
        var undone = await sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-001",
            Action = "Add"
        });
        undone.IsFavourited.Should().BeTrue();
        undone.ActionApplied.Should().Be("Added");
        undone.TotalFavourites.Should().Be(1);
        undone.SyncVersion.Should().Be(3);
    }

    [Fact]
    public async Task Toggle_NullOrToggleAction_SwitchesStateAutomatically()
    {
        var (sut, _) = CreateSut();

        var first = await sut.ToggleAsync("user-toggle", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-002",
            Action = null
        });
        first.IsFavourited.Should().BeTrue();
        first.ActionApplied.Should().Be("Added");

        var second = await sut.ToggleAsync("user-toggle", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-002",
            Action = "Toggle"
        });
        second.IsFavourited.Should().BeFalse();
        second.ActionApplied.Should().Be("Removed");
    }

    [Xunit.Theory]
    [InlineData("Delete")]
    [InlineData("Invalid")]
    public async Task Toggle_InvalidAction_ThrowsValidationException(string invalidAction)
    {
        var (sut, _) = CreateSut();

        var act = () => sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-2026-001",
            Action = invalidAction
        });

        await act.Should().ThrowAsync<FavouriteValidationException>();
    }

    [Fact]
    public async Task Toggle_AddUnknownListing_ThrowsNotFoundException()
    {
        var (sut, _) = CreateSut();

        var act = () => sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-does-not-exist",
            Action = "Add"
        });

        await act.Should().ThrowAsync<FavouriteListingNotFoundException>();
    }

    [Fact]
    public async Task Toggle_RemoveUnknownListing_IsIdempotentWithoutError()
    {
        var (sut, _) = CreateSut();

        var result = await sut.ToggleAsync("user-1", new ToggleFavouriteRequest
        {
            ListingId = "room-does-not-exist",
            Action = "Remove"
        });

        result.IsFavourited.Should().BeFalse();
        result.ActionApplied.Should().Be("NoChange");
        result.TotalFavourites.Should().Be(0);
        result.CurrentRent.Should().BeNull();
        result.CurrentStatus.Should().BeNull();
    }

    [Fact]
    public async Task GetFavourites_ReflectsLivePriceChangeAndRentedStatus_AndSupportsIncludeUnavailableFlag()
    {
        var listings = ListingSeedData.Create().ToList();
        var (sut, _) = CreateSut(listings);

        await sut.ToggleAsync("user-live", new ToggleFavouriteRequest { ListingId = "room-2026-001", Action = "Add" });
        await sut.ToggleAsync("user-live", new ToggleFavouriteRequest { ListingId = "room-2026-004", Action = "Add" });

        // Giả lập chủ trọ tăng giá phòng 001 và phòng 004 đang Rented
        var room1 = listings.Single(l => l.Id == "room-2026-001");
        room1.Rent = 1_950_000m;

        var all = await sut.GetFavouritesAsync("user-live", includeUnavailable: true);
        all.TotalCount.Should().Be(2);

        var fav1 = all.Items.Single(i => i.ListingId == "room-2026-001");
        fav1.SavedRent.Should().Be(1_800_000m);
        fav1.Rent.Should().Be(1_950_000m);
        fav1.HasPriceChanged.Should().BeTrue();
        fav1.Status.Should().Be("Available");
        fav1.IsAvailable.Should().BeTrue();

        var fav4 = all.Items.Single(i => i.ListingId == "room-2026-004");
        fav4.Status.Should().Be("Rented");
        fav4.IsAvailable.Should().BeFalse();

        // Khi lọc bỏ phòng đã thuê
        var availableOnly = await sut.GetFavouritesAsync("user-live", includeUnavailable: false);
        availableOnly.TotalCount.Should().Be(1);
        availableOnly.Items.Should().ContainSingle(i => i.ListingId == "room-2026-001");
    }

    [Fact]
    public async Task Sync_MultiDeviceMerge_DeduplicatesSkipsUnknownAndIsolatesUsers()
    {
        var (sut, _) = CreateSut();

        // Thiết bị 1 của User A đã lưu room-2026-001 và room-2026-002
        await sut.ToggleAsync("user-a", new ToggleFavouriteRequest { ListingId = "room-2026-001", Action = "Add" });
        await sut.ToggleAsync("user-a", new ToggleFavouriteRequest { ListingId = "room-2026-002", Action = "Add" });

        // Thiết bị 2 của User A đồng bộ: thêm 001 (trùng), 003 (mới), ghost (không tồn tại) và xóa 002
        var syncedA = await sut.SyncAsync("user-a", new SyncFavouritesRequest
        {
            ListingIds = ["room-2026-001", "room-2026-003", "room-2026-003", "ghost-room"],
            RemovedListingIds = ["room-2026-002"]
        });

        syncedA.AddedCount.Should().Be(1);
        syncedA.RemovedCount.Should().Be(1);
        syncedA.TotalCount.Should().Be(2);
        syncedA.SkippedListingIds.Should().ContainSingle().Which.Should().Be("ghost-room");
        syncedA.Items.Select(i => i.ListingId).Should().BeEquivalentTo("room-2026-001", "room-2026-003");
        syncedA.SyncVersion.Should().Be(3);

        // User B hoàn toàn độc lập với User A
        var listB = await sut.GetFavouritesAsync("user-b");
        listB.TotalCount.Should().Be(0);
        listB.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Toggle_ConcurrentAddsForSameUser_PreservesAllItemsWithoutLostUpdates()
    {
        var (sut, _) = CreateSut();
        string[] roomIds = ["room-2026-001", "room-2026-002", "room-2026-003", "room-2026-004", "room-2026-005"];

        await Task.WhenAll(roomIds.Select(id =>
            sut.ToggleAsync("user-concurrent", new ToggleFavouriteRequest
            {
                ListingId = id,
                Action = "Add"
            })));

        var list = await sut.GetFavouritesAsync("user-concurrent");
        list.TotalCount.Should().Be(5);
        list.Items.Select(i => i.ListingId).Should().BeEquivalentTo(roomIds);
    }

    [Fact]
    public async Task Controller_MissingIdentity_Returns401UnauthorizedAcrossEndpoints()
    {
        var (sut, _) = CreateSut();
        var controller = CreateController(sut);

        var toggleRes = await controller.Toggle(
            new ToggleFavouriteRequest { ListingId = "room-2026-001", Action = "Add" },
            CancellationToken.None);
        var getRes = await controller.GetFavourites();
        var syncRes = await controller.Sync(new SyncFavouritesRequest(), CancellationToken.None);
        var deleteRes = await controller.Remove("room-2026-001", CancellationToken.None);

        toggleRes.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);
        getRes.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);
        syncRes.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);
        deleteRes.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Controller_ValidHeaderIdentity_ReturnsExpected200_400_404Responses()
    {
        var (sut, _) = CreateSut();
        var controller = CreateController(sut, headerUserId: "student-ictu-01");

        var toggleOk = await controller.Toggle(
            new ToggleFavouriteRequest { ListingId = "room-2026-001", Action = "Add" },
            CancellationToken.None);
        toggleOk.Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);

        var listOk = await controller.GetFavourites();
        var listPayload = listOk.Result.Should().BeOfType<OkObjectResult>().Which
            .Value.Should().BeOfType<FavouritesListResponse>().Subject;
        listPayload.UserId.Should().Be("student-ictu-01");
        listPayload.TotalCount.Should().Be(1);

        var syncOk = await controller.Sync(
            new SyncFavouritesRequest { ListingIds = ["room-2026-002"] },
            CancellationToken.None);
        syncOk.Result.Should().BeOfType<OkObjectResult>().Which
            .Value.Should().BeOfType<SyncFavouritesResponse>().Subject.TotalCount.Should().Be(2);

        var removeOk = await controller.Remove("room-2026-001", CancellationToken.None);
        removeOk.Result.Should().BeOfType<OkObjectResult>().Which
            .Value.Should().BeOfType<ToggleFavouriteResponse>().Subject.TotalFavourites.Should().Be(1);

        var badRequest = await controller.Toggle(
            new ToggleFavouriteRequest { ListingId = "room-2026-001", Action = "Bad" },
            CancellationToken.None);
        badRequest.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);

        var notFound = await controller.Toggle(
            new ToggleFavouriteRequest { ListingId = "missing-room", Action = "Add" },
            CancellationToken.None);
        notFound.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }
}
