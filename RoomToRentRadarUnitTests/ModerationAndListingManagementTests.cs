using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class ModerationAndListingManagementTests
{
    private static (
        InMemoryListingRepository ListingRepo,
        InMemoryModerationAuditRepository AuditRepo,
        ListingModerationService ModerationSvc,
        ListingService PublicListingSvc,
        ListingSearchService PublicSearchSvc) CreateSut()
    {
        var listingRepo = new InMemoryListingRepository(ListingSeedData.Create());
        var auditRepo = new InMemoryModerationAuditRepository();
        var moderationSvc = new ListingModerationService(listingRepo, auditRepo);
        var publicListingSvc = new ListingService(listingRepo);
        var publicSearchSvc = new ListingSearchService(listingRepo);
        return (listingRepo, auditRepo, moderationSvc, publicListingSvc, publicSearchSvc);
    }

    private static ModerationController CreateModerationController(
        IListingModerationService service,
        string? userId = null,
        string? headerRole = null,
        string? claimRole = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userId is not null)
        {
            httpContext.Request.Headers[ControllerIdentityHelper.UserIdHeader] = userId;
        }

        if (headerRole is not null)
        {
            httpContext.Request.Headers[ControllerIdentityHelper.UserRoleHeader] = headerRole;
        }

        if (claimRole is not null && userId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, claimRole)],
                "Bearer"));
        }

        return new ModerationController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static LandlordListingsController CreateLandlordController(
        IListingModerationService service,
        string? landlordId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (landlordId is not null)
        {
            httpContext.Request.Headers[ControllerIdentityHelper.UserIdHeader] = landlordId;
        }

        return new LandlordListingsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public async Task PendingAndRejectedListings_AreHiddenFromPublicApis_UntilModeratorApproves()
    {
        var (_, _, modSvc, publicListingSvc, publicSearchSvc) = CreateSut();

        // Trước khi duyệt: room-2026-006 (PendingReview) và room-2026-007 (Rejected) không được xuất hiện công khai
        var beforePaged = await publicListingSvc.GetListingsAsync(new ListingQueryRequest { PageSize = 50 });
        var beforeSearch = await publicSearchSvc.SearchAsync(new ListingSearchQuery { PageSize = 50 });
        beforePaged.Items.Select(i => i.Id).Should().NotContain(["room-2026-006", "room-2026-007"]);
        beforeSearch.Items.Select(i => i.Id).Should().NotContain(["room-2026-006", "room-2026-007"]);

        // Moderator duyệt room-2026-006
        var approved = await modSvc.ModerateListingAsync(
            "room-2026-006",
            "mod-01",
            new ModerationDecisionRequest { Decision = "Approve", Note = "Đủ ảnh và đúng địa chỉ ICTU" });

        approved.ModerationStatus.Should().Be(ListingModerationStatus.Approved);
        approved.Status.Should().Be(ListingAvailabilityStatus.Available);
        approved.IsVerified.Should().BeTrue();
        approved.AuditLog.Action.Should().Be("Approve");
        approved.AuditLog.PreviousModerationStatus.Should().Be(ListingModerationStatus.PendingReview);
        approved.AuditLog.NewModerationStatus.Should().Be(ListingModerationStatus.Approved);

        // Sau khi duyệt: xuất hiện ngay trong cả Listing API và Search API
        var afterPaged = await publicListingSvc.GetListingsAsync(new ListingQueryRequest { PageSize = 50 });
        var afterSearch = await publicSearchSvc.SearchAsync(new ListingSearchQuery { PageSize = 50 });
        afterPaged.Items.Select(i => i.Id).Should().Contain("room-2026-006");
        afterSearch.Items.Select(i => i.Id).Should().Contain("room-2026-006");
    }

    [Fact]
    public async Task Moderate_RejectRequiresNote_AndHidesApprovedListingFromPublicQueries()
    {
        var (_, _, modSvc, publicListingSvc, _) = CreateSut();

        var missingNote = () => modSvc.ModerateListingAsync(
            "room-2026-001",
            "mod-01",
            new ModerationDecisionRequest { Decision = "Reject", Note = "   " });
        await missingNote.Should().ThrowAsync<ListingManagementValidationException>();

        var rejected = await modSvc.ModerateListingAsync(
            "room-2026-001",
            "mod-01",
            new ModerationDecisionRequest { Decision = "Reject", Note = "Sai giá thuê thực tế" });

        rejected.ModerationStatus.Should().Be(ListingModerationStatus.Rejected);
        rejected.Status.Should().Be(ListingAvailabilityStatus.Rejected);
        rejected.IsVerified.Should().BeFalse();

        var afterPaged = await publicListingSvc.GetListingsAsync(new ListingQueryRequest { PageSize = 50 });
        afterPaged.Items.Select(i => i.Id).Should().NotContain("room-2026-001");

        var history = await modSvc.GetModerationHistoryAsync("room-2026-001");
        history.Should().ContainSingle();
        history[0].Action.Should().Be("Reject");
        history[0].Note.Should().Be("Sai giá thuê thực tế");
    }

    [Fact]
    public async Task ModerationQueue_FiltersAndCountsAndTracksLatestNote()
    {
        var (_, _, modSvc, _, _) = CreateSut();

        var pending = await modSvc.GetModerationQueueAsync(ListingModerationStatus.PendingReview);
        pending.PendingCount.Should().Be(1);
        pending.ApprovedCount.Should().Be(5);
        pending.RejectedCount.Should().Be(1);
        pending.Items.Should().ContainSingle(i => i.ListingId == "room-2026-006");

        await modSvc.ModerateListingAsync(
            "room-2026-006",
            "mod-01",
            new ModerationDecisionRequest { Decision = "Approve", Note = "Hợp lệ" });

        var approved = await modSvc.GetModerationQueueAsync(ListingModerationStatus.Approved);
        approved.PendingCount.Should().Be(0);
        approved.ApprovedCount.Should().Be(6);
        approved.Items.Single(i => i.ListingId == "room-2026-006").LatestModerationNote.Should().Be("Hợp lệ");
    }

    [Fact]
    public async Task Landlord_ViewsOwnListingsAcrossStatuses_AndManagesAvailabilitySafely()
    {
        var (_, _, modSvc, publicListingSvc, _) = CreateSut();

        // Cô Lan (usr-landlord-09) có room-001 (Available), room-004 (Rented), room-006 (PendingReview)
        var own = await modSvc.GetLandlordListingsAsync("usr-landlord-09");
        own.TotalCount.Should().Be(3);
        own.AvailableCount.Should().Be(1);
        own.RentedCount.Should().Be(1);
        own.PendingReviewCount.Should().Be(1);
        own.Items.Select(i => i.ListingId).Should().BeEquivalentTo("room-2026-001", "room-2026-004", "room-2026-006");

        // Chặn chủ trọ tự bật Available cho phòng PendingReview (US08 AC2)
        var selfApprove = () => modSvc.UpdateLandlordAvailabilityAsync(
            "room-2026-006",
            "usr-landlord-09",
            new UpdateListingAvailabilityRequest { Status = "Available" });
        await selfApprove.Should().ThrowAsync<ListingManagementValidationException>();

        // Cho phép chủ trọ chuyển phòng đã duyệt room-2026-001 sang Rented -> ẩn khỏi public
        var toRented = await modSvc.UpdateLandlordAvailabilityAsync(
            "room-2026-001",
            "usr-landlord-09",
            new UpdateListingAvailabilityRequest { Status = "Rented" });
        toRented.Status.Should().Be(ListingAvailabilityStatus.Rented);
        toRented.IsVerified.Should().BeTrue();

        var publicAfterRented = await publicListingSvc.GetListingsAsync(new ListingQueryRequest { PageSize = 50 });
        publicAfterRented.Items.Select(i => i.Id).Should().NotContain("room-2026-001");

        // Chủ trọ khác không thể sửa phòng của Cô Lan -> 403 Forbidden
        var otherLandlordEdit = () => modSvc.UpdateLandlordAvailabilityAsync(
            "room-2026-001",
            "usr-landlord-10",
            new UpdateListingAvailabilityRequest { Status = "Hidden" });
        await otherLandlordEdit.Should().ThrowAsync<ListingManagementForbiddenException>();

        // Cô Lan mở lại Available cho room-2026-001 (đã Approved) thành công
        var backAvailable = await modSvc.UpdateLandlordAvailabilityAsync(
            "room-2026-001",
            "usr-landlord-09",
            new UpdateListingAvailabilityRequest { Status = "Available" });
        backAvailable.Status.Should().Be(ListingAvailabilityStatus.Available);
    }

    [Fact]
    public async Task Landlord_ArchiveListing_HidesFromBothLandlordAndPublic()
    {
        var (_, _, modSvc, publicListingSvc, _) = CreateSut();

        var otherArchive = () => modSvc.ArchiveLandlordListingAsync("room-2026-002", "usr-landlord-09");
        await otherArchive.Should().ThrowAsync<ListingManagementForbiddenException>();

        var archived = await modSvc.ArchiveLandlordListingAsync("room-2026-002", "usr-landlord-10");
        archived.Status.Should().Be(ListingAvailabilityStatus.Archived);
        archived.IsVerified.Should().BeFalse();

        var landlordAfter = await modSvc.GetLandlordListingsAsync("usr-landlord-10");
        landlordAfter.Items.Select(i => i.ListingId).Should().NotContain("room-2026-002");

        var publicAfter = await publicListingSvc.GetListingsAsync(new ListingQueryRequest { PageSize = 50 });
        publicAfter.Items.Select(i => i.Id).Should().NotContain("room-2026-002");
    }

    [Fact]
    public async Task Controllers_EnforceAuthRolesAndReturnProperHttpCodes()
    {
        var (_, _, modSvc, _, _) = CreateSut();

        // 401 khi thiếu user
        var anonMod = CreateModerationController(modSvc);
        (await anonMod.GetQueue()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);

        // 403 khi role không phải Moderator/Admin
        var studentMod = CreateModerationController(modSvc, userId: "student-1", headerRole: "Student");
        (await studentMod.GetQueue()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);

        // 200 khi là Moderator hợp lệ
        var mod = CreateModerationController(modSvc, userId: "mod-01", headerRole: "Moderator");
        var queueOk = await mod.GetQueue(ListingModerationStatus.PendingReview);
        queueOk.Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);

        // 404 khi phòng không tồn tại
        var mod404 = await mod.Decide("missing-room", new ModerationDecisionRequest { Decision = "Approve" });
        mod404.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);

        // 400 khi Reject thiếu lý do
        var mod400 = await mod.Decide("room-2026-006", new ModerationDecisionRequest { Decision = "Reject", Note = "" });
        mod400.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);

        // 200 khi Approve qua controller + 200 khi xem history
        var mod200 = await mod.Decide("room-2026-006", new ModerationDecisionRequest { Decision = "Approve", Note = "OK" });
        mod200.Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);
        var hist200 = await mod.GetHistory("room-2026-006");
        hist200.Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);

        // Landlord controller: 401 khi thiếu header, 403 khi khác chủ, 200 khi đúng chủ
        var anonLandlord = CreateLandlordController(modSvc);
        (await anonLandlord.GetMyListings()).Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(401);

        var landlord = CreateLandlordController(modSvc, landlordId: "usr-landlord-09");
        (await landlord.GetMyListings()).Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);
        (await landlord.UpdateAvailability("room-2026-002", new UpdateListingAvailabilityRequest { Status = "Hidden" }))
            .Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        (await landlord.UpdateAvailability("room-2026-006", new UpdateListingAvailabilityRequest { Status = "Hidden" }))
            .Result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(200);
    }
}
