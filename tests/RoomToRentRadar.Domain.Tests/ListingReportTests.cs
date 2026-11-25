using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Tests;

[TestFixture]
public class ListingReportTests
{
    [Test]
    public void Create_WithValidInput_SetsPendingStatusAndTrimsFields()
    {
        var now = DateTimeOffset.UtcNow;
        var reporterId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        var report = ListingReport.Create(
            "  room-101  ",
            "  Phòng trọ gần ICTU  ",
            landlordId,
            reporterId,
            ReportReason.InaccurateInfo,
            "  Giá thực tế cao hơn niêm yết  ",
            now);

        Assert.Multiple(() =>
        {
            Assert.That(report.Id, Does.StartWith("report:"));
            Assert.That(report.RoomId, Is.EqualTo("room-101"));
            Assert.That(report.RoomTitleSnapshot, Is.EqualTo("Phòng trọ gần ICTU"));
            Assert.That(report.RoomLandlordId, Is.EqualTo(landlordId));
            Assert.That(report.ReporterUserId, Is.EqualTo(reporterId));
            Assert.That(report.Reason, Is.EqualTo(ReportReason.InaccurateInfo));
            Assert.That(report.Note, Is.EqualTo("Giá thực tế cao hơn niêm yết"));
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Pending));
            Assert.That(report.CreatedAtUtc, Is.EqualTo(now));
        });
    }

    [Test]
    public void Create_WithOtherReasonAndMissingNote_ThrowsReportDomainException()
    {
        Assert.Throws<ReportDomainException>(() => ListingReport.Create(
            "room-101",
            "Phòng trọ",
            null,
            Guid.NewGuid(),
            ReportReason.Other,
            "   ",
            DateTimeOffset.UtcNow));
    }

    [Test]
    public void Create_WithNoteExceedingMaxLength_ThrowsReportDomainException()
    {
        var tooLong = new string('a', ListingReport.MaxNoteLength + 1);
        Assert.Throws<ReportDomainException>(() => ListingReport.Create(
            "room-101",
            "Phòng trọ",
            null,
            Guid.NewGuid(),
            ReportReason.SuspectedScam,
            tooLong,
            DateTimeOffset.UtcNow));
    }

    [Test]
    public void ApplyReviewDecision_ValidTransitions_UpdatesAuditFields()
    {
        var created = DateTimeOffset.UtcNow.AddMinutes(-10);
        var report = ListingReport.Create("room-1", "Phòng 1", null, Guid.NewGuid(), ReportReason.SuspectedScam, null, created);
        var adminId = Guid.NewGuid();

        var reviewStart = created.AddMinutes(5);
        report.ApplyReviewDecision(ReportStatus.InReview, adminId, null, reviewStart);
        Assert.That(report.Status, Is.EqualTo(ReportStatus.InReview));

        var resolvedAt = created.AddMinutes(8);
        report.ApplyReviewDecision(ReportStatus.Resolved, adminId, "Đã xác minh và gỡ tin giả", resolvedAt);

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ReportStatus.Resolved));
            Assert.That(report.ReviewedByUserId, Is.EqualTo(adminId));
            Assert.That(report.ReviewedAtUtc, Is.EqualTo(resolvedAt));
            Assert.That(report.UpdatedAtUtc, Is.EqualTo(resolvedAt));
            Assert.That(report.ResolutionNote, Is.EqualTo("Đã xác minh và gỡ tin giả"));
        });
    }

    [Test]
    public void ApplyReviewDecision_InvalidTransitionFromTerminalState_ThrowsReportDomainException()
    {
        var report = ListingReport.Create("room-1", "Phòng 1", null, Guid.NewGuid(), ReportReason.AlreadyRented, null, DateTimeOffset.UtcNow);
        var adminId = Guid.NewGuid();
        report.ApplyReviewDecision(ReportStatus.Dismissed, adminId, "Tin vẫn còn phòng", DateTimeOffset.UtcNow);

        Assert.Throws<ReportDomainException>(() =>
            report.ApplyReviewDecision(ReportStatus.Resolved, adminId, "Thử đổi lại", DateTimeOffset.UtcNow));
    }

    [Test]
    public void ApplyReviewDecision_ResolveWithoutResolutionNote_ThrowsReportDomainException()
    {
        var report = ListingReport.Create("room-1", "Phòng 1", null, Guid.NewGuid(), ReportReason.AlreadyRented, null, DateTimeOffset.UtcNow);
        Assert.Throws<ReportDomainException>(() =>
            report.ApplyReviewDecision(ReportStatus.Resolved, Guid.NewGuid(), " ", DateTimeOffset.UtcNow));
    }
}
