using NUnit.Framework;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ReportListingPopupViewModelTests
{
    [Test]
    public void TryBuildResult_WhenNoReasonSelected_SetsValidationErrorAndReturnsNull()
    {
        var vm = new ReportListingPopupViewModel("Cozy Studio");

        var result = vm.TryBuildResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(vm.HasValidationError, Is.True);
            Assert.That(vm.ValidationError, Is.EqualTo("Please select a reason for reporting this listing."));
        });
    }

    [Test]
    public void TryBuildResult_WhenOtherSelectedWithoutNote_RequiresNote()
    {
        var vm = new ReportListingPopupViewModel("Cozy Studio");
        var other = vm.Reasons.Single(r => r.Code == ReportReasonCode.Other);
        vm.SelectReason(other);

        var result = vm.TryBuildResult();

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsNoteRequired, Is.True);
            Assert.That(result, Is.Null);
            Assert.That(vm.HasValidationError, Is.True);
            Assert.That(vm.ValidationError, Does.Contain("Other reason"));
        });
    }

    [Test]
    public void TryBuildResult_WhenNoteExceedsMaxLength_ReturnsNullWithError()
    {
        var vm = new ReportListingPopupViewModel("Cozy Studio");
        vm.SelectReason(vm.Reasons.First());
        vm.Note = new string('a', ReportListingPopupViewModel.MaxNoteLength + 1);

        var result = vm.TryBuildResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(vm.HasValidationError, Is.True);
            Assert.That(vm.ValidationError, Does.Contain("500"));
        });
    }

    [Test]
    public void TryBuildResult_WhenValidReasonAndNote_ReturnsTrimmedResult()
    {
        var vm = new ReportListingPopupViewModel("Cozy Studio");
        var scam = vm.Reasons.Single(r => r.Code == ReportReasonCode.SuspectedScam);
        vm.SelectReason(scam);
        vm.Note = "  Asked for deposit before viewing  ";

        var result = vm.TryBuildResult();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Reason, Is.EqualTo(ReportReasonCode.SuspectedScam));
            Assert.That(result.Note, Is.EqualTo("Asked for deposit before viewing"));
            Assert.That(vm.HasValidationError, Is.False);
            Assert.That(scam.IsSelected, Is.True);
        });
    }
}
