using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomDetailViewModelTests
{
    private sealed class RecordingNavigationService : IAppNavigationService
    {
        public string? LastAlertTitle { get; private set; }
        public string? LastAlertMessage { get; private set; }

        public Task NavigateToRoomDetailAsync(string roomId, bool? isAvailable = null) => Task.CompletedTask;

        public Task ShowAlertAsync(string title, string message, string cancel = "Đóng")
        {
            LastAlertTitle = title;
            LastAlertMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class LiveFlagRoomDetailService(bool isFromLiveApi) : IRoomDetailService
    {
        private readonly MockRoomDetailService _inner = new();

        public async Task<RoomDetailFetchResult> GetRoomDetailAsync(
            string roomId,
            bool isAuthenticated = false,
            CancellationToken cancellationToken = default)
        {
            var inner = await _inner.GetRoomDetailAsync(roomId, isAuthenticated, cancellationToken);
            return new RoomDetailFetchResult(inner.Detail, isFromLiveApi);
        }
    }

    [Test]
    public void Constructor_LoadsDefaultAvailableRoomWithAmenitiesRulesAndPhotos()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());

        Assert.Multiple(() =>
        {
            Assert.That(vm.RoomId, Is.EqualTo("room-2026-001"));
            Assert.That(vm.IsAvailable, Is.True);
            Assert.That(vm.IsUnavailable, Is.False);
            Assert.That(vm.HasError, Is.False);
            Assert.That(vm.Title, Does.Contain("ICTU"));
            Assert.That(vm.Rent, Is.EqualTo(1_800_000m));
            Assert.That(vm.Deposit, Is.EqualTo(1_800_000m));
            Assert.That(vm.AmenityChips, Is.Not.Empty);
            Assert.That(vm.HouseRules.QuietHours, Is.EqualTo("23:00 - 05:30"));
            Assert.That(vm.RoomPhotos, Has.Count.EqualTo(4));
            Assert.That(vm.DisplayedLandlordPhone, Does.Contain("098***"));
            Assert.That(vm.LandlordContactButtonText, Does.Contain("Đăng nhập"));
            Assert.That(vm.SimilarRooms, Is.Empty);
        });
    }

    [Test]
    public async Task LoadRoomDetailAsync_DungAliasId_ResolvesCanonicalRoomData()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());

        await vm.LoadRoomDetailAsync("room-ictu-001");

        Assert.Multiple(() =>
        {
            Assert.That(vm.RoomId, Is.EqualTo("room-ictu-001"));
            Assert.That(vm.Title, Does.Contain("Studio Ban Công"));
            Assert.That(vm.Rent, Is.EqualTo(1_800_000m));
            Assert.That(vm.HasError, Is.False);
            Assert.That(vm.IsAvailable, Is.True);
        });
    }

    [Test]
    public async Task LoadRoomDetailAsync_UnavailableRoom_ShowsUnavailableBannerAndThreeSimilarRooms()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());

        await vm.LoadRoomDetailAsync("room-2026-004");

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsAvailable, Is.False);
            Assert.That(vm.IsUnavailable, Is.True);
            Assert.That(vm.UnavailableMessage, Is.Not.Empty);
            Assert.That(vm.SimilarRooms, Has.Count.EqualTo(3));
            Assert.That(vm.SimilarRooms, Has.All.Matches<SimilarRoomItemDto>(r => r.IsVerified));
        });
    }

    [Test]
    public async Task SelectSimilarRoomCommand_SwitchesToSelectedAvailableRoom()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());
        await vm.LoadRoomDetailAsync("room-2026-004");
        var candidate = vm.SimilarRooms[0];

        await vm.SelectSimilarRoomCommand.ExecuteAsync(candidate);

        Assert.Multiple(() =>
        {
            Assert.That(vm.RoomId, Is.EqualTo(candidate.Id));
            Assert.That(vm.Title, Is.EqualTo(candidate.Title));
            Assert.That(vm.IsAvailable, Is.True);
            Assert.That(vm.SimilarRooms, Is.Empty);
        });
    }

    [Test]
    public async Task ContactOrSignInCommand_WhenAnonymous_AuthenticatesAndRevealsPhone()
    {
        var nav = new RecordingNavigationService();
        var vm = new RoomDetailViewModel(new MockRoomDetailService(), nav);

        await vm.ContactOrSignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsAuthenticated, Is.True);
            Assert.That(vm.RawLandlordPhone, Is.EqualTo("0987654321"));
            Assert.That(vm.DisplayedLandlordPhone, Does.Contain("0987654321"));
            Assert.That(vm.LandlordContactButtonText, Does.Contain("0987654321"));
            Assert.That(nav.LastAlertTitle, Is.Not.Null);
            Assert.That(nav.LastAlertMessage, Does.Contain("0987654321"));
        });
    }

    [Test]
    public void SelectPhotoCommand_UpdatesHeroImageUrlAndActiveThumbnail()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());
        var secondPhoto = vm.RoomPhotos[1];

        vm.SelectPhotoCommand.Execute(secondPhoto);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HeroImageUrl, Is.EqualTo(secondPhoto.ImageUrl));
            Assert.That(vm.RoomPhotos[0].IsSelected, Is.False);
            Assert.That(vm.RoomPhotos[1].IsSelected, Is.True);
        });
    }

    [Test]
    public async Task LoadRoomDetailAsync_UnknownRoomId_SetsErrorStateAndClearsSimilarRooms()
    {
        var vm = new RoomDetailViewModel(new MockRoomDetailService());

        await vm.LoadRoomDetailAsync("room-does-not-exist");

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasError, Is.True);
            Assert.That(vm.ErrorMessage, Does.Contain("room-does-not-exist"));
            Assert.That(vm.SimilarRooms, Is.Empty);
        });
    }

    [Test]
    public async Task CalculateDistanceMeters_AndLiveApiBadge_BehaveAsExpected()
    {
        var vm = new RoomDetailViewModel(new LiveFlagRoomDetailService(isFromLiveApi: true));
        await vm.LoadRoomDetailAsync("room-2026-002");

        var selfDistance = RoomDetailViewModel.CalculateDistanceMeters(21.5850, 105.8066, 21.5850, 105.8066);
        var northDistance = RoomDetailViewModel.CalculateDistanceMeters(21.5886, 105.8066, 21.5850, 105.8066);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFromLiveApi, Is.True);
            Assert.That(vm.DataSourceBadgeText, Does.Contain("API T02.1"));
            Assert.That(selfDistance, Is.EqualTo(0));
            Assert.That(northDistance, Is.InRange(390, 410));
        });
    }
}
