using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HouseRulesSectionViewModelTests
{
    private sealed class LiveFlagHouseRulesService(bool isFromLiveApi) : IHouseRulesService
    {
        private readonly MockHouseRulesService _inner = new();

        public async Task<HouseRulesUpdateResult> UpdateHouseRulesAsync(
            string listingId,
            UpdateHouseRulesRequestContract request,
            HouseRulesResponseContract? currentState = null,
            CancellationToken cancellationToken = default)
        {
            var res = await _inner.UpdateHouseRulesAsync(listingId, request, currentState, cancellationToken);
            return new HouseRulesUpdateResult(res.Rules, isFromLiveApi);
        }
    }

    [Test]
    public void Constructor_InitializesVersionTwoAndSeedsVersionOneHistory()
    {
        var vm = new HouseRulesSectionViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Version, Is.EqualTo(2));
            Assert.That(vm.VersionBadgeText, Does.Contain("v2"));
            Assert.That(vm.IsEditing, Is.False);
            Assert.That(vm.IsNotEditing, Is.True);
            Assert.That(vm.HasHistory, Is.True);
            Assert.That(vm.VersionHistory, Has.Count.EqualTo(1));
            Assert.That(vm.VersionHistory[0].Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void ToggleEditModeCommand_EntersEditModeAndCopiesCurrentRulesToDrafts()
    {
        var vm = new HouseRulesSectionViewModel();

        vm.ToggleEditModeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsEditing, Is.True);
            Assert.That(vm.IsNotEditing, Is.False);
            Assert.That(vm.EditButtonText, Is.EqualTo("Đóng chỉnh sửa"));
            Assert.That(vm.DraftQuietHours, Is.EqualTo(vm.QuietHours));
            Assert.That(vm.DraftGuestPolicy, Is.EqualTo(vm.GuestPolicy));
            Assert.That(vm.DraftCookingRules, Is.EqualTo(vm.CookingRules));
            Assert.That(vm.DraftParkingRules, Is.EqualTo(vm.ParkingRules));
        });
    }

    [Test]
    public void CancelEditCommand_DiscardsDraftChangesAndExitsEditMode()
    {
        var vm = new HouseRulesSectionViewModel();
        var originalQuietHours = vm.QuietHours;
        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftQuietHours = "21:00 - 06:00";

        vm.CancelEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsEditing, Is.False);
            Assert.That(vm.QuietHours, Is.EqualTo(originalQuietHours));
            Assert.That(vm.DraftQuietHours, Is.EqualTo(originalQuietHours));
            Assert.That(vm.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task SaveRulesCommand_EmptyField_SetsValidationErrorAndDoesNotIncrementVersion()
    {
        var vm = new HouseRulesSectionViewModel();
        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftCookingRules = "   ";

        await vm.SaveRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasValidationError, Is.True);
            Assert.That(vm.ValidationError, Does.Contain("đầy đủ cả 4 mục"));
            Assert.That(vm.Version, Is.EqualTo(2));
            Assert.That(vm.IsEditing, Is.True);
        });
    }

    [Test]
    public async Task SaveRulesCommand_ValidDrafts_IncrementsVersionAndArchivesPreviousVersion()
    {
        var vm = new HouseRulesSectionViewModel(new MockHouseRulesService());
        var previousQuietHours = vm.QuietHours;
        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftQuietHours = "23:30 - 05:30";
        vm.DraftCookingRules = "Được nấu ăn tự do, giữ vệ sinh khu bếp chung";

        await vm.SaveRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasValidationError, Is.False);
            Assert.That(vm.IsEditing, Is.False);
            Assert.That(vm.Version, Is.EqualTo(3));
            Assert.That(vm.VersionBadgeText, Does.Contain("v3"));
            Assert.That(vm.QuietHours, Is.EqualTo("23:30 - 05:30"));
            Assert.That(vm.VersionHistory, Has.Count.EqualTo(2));
            Assert.That(vm.VersionHistory[0].Version, Is.EqualTo(2));
            Assert.That(vm.VersionHistory[0].QuietHours, Is.EqualTo(previousQuietHours));
            Assert.That(vm.VersionHistory[1].Version, Is.EqualTo(1));
            Assert.That(vm.StatusMessage, Does.Contain("v3"));
        });
    }

    [Test]
    public async Task SaveRulesCommand_SequentialUpdates_BuildsDescendingHistoryChain()
    {
        var vm = new HouseRulesSectionViewModel(new MockHouseRulesService());

        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftQuietHours = "23:30 - 05:30";
        await vm.SaveRulesCommand.ExecuteAsync(null);

        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftQuietHours = "00:00 - 05:30";
        await vm.SaveRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Version, Is.EqualTo(4));
            Assert.That(vm.VersionHistory.Select(h => h.Version), Is.EqualTo(new[] { 3, 2, 1 }));
        });
    }

    [Test]
    public async Task SaveRulesCommand_LiveApiFlag_UpdatesDataSourceBadgeText()
    {
        var vm = new HouseRulesSectionViewModel(new LiveFlagHouseRulesService(isFromLiveApi: true));
        vm.ToggleEditModeCommand.Execute(null);
        vm.DraftGuestPolicy = "Khách đăng ký trước với bảo vệ";

        await vm.SaveRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsFromLiveApi, Is.True);
            Assert.That(vm.DataSourceBadgeText, Does.Contain("API T11.1"));
        });
    }

    [Test]
    public void ToggleHistoryCommand_TogglesHistoryAccordion()
    {
        var vm = new HouseRulesSectionViewModel();

        Assert.That(vm.IsHistoryExpanded, Is.False);
        vm.ToggleHistoryCommand.Execute(null);
        Assert.That(vm.IsHistoryExpanded, Is.True);
        Assert.That(vm.HistoryToggleText, Does.Contain("Ẩn lịch sử"));
        vm.ToggleHistoryCommand.Execute(null);
        Assert.That(vm.IsHistoryExpanded, Is.False);
    }

    [Test]
    public async Task RestoreVersionCommand_AppliesSelectedSnapshotAsNewIncrementedVersion()
    {
        var vm = new HouseRulesSectionViewModel(new MockHouseRulesService());
        var versionOneSnapshot = vm.VersionHistory.First(h => h.Version == 1);

        await vm.RestoreVersionCommand.ExecuteAsync(versionOneSnapshot);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Version, Is.EqualTo(3));
            Assert.That(vm.QuietHours, Is.EqualTo(versionOneSnapshot.QuietHours));
            Assert.That(vm.GuestPolicy, Is.EqualTo(versionOneSnapshot.GuestPolicy));
            Assert.That(vm.CookingRules, Is.EqualTo(versionOneSnapshot.CookingRules));
            Assert.That(vm.ParkingRules, Is.EqualTo(versionOneSnapshot.ParkingRules));
            Assert.That(vm.VersionHistory, Has.Count.EqualTo(2));
            Assert.That(vm.VersionHistory[0].Version, Is.EqualTo(2));
        });
    }
}
