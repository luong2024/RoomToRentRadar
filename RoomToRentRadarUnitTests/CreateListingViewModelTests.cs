using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class CreateListingViewModelTests
{
    private sealed class RecordingLandlordListingService(bool isFromLiveApi = false) : ILandlordListingService
    {
        public CreateListingRequestContract? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public Task<CreateListingSubmitResult> SubmitListingAsync(
            CreateListingRequestContract request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            var response = new CreateListingResponseContract
            {
                ListingId = "room-pending-2026-test",
                Status = "PendingReview",
                IsVerified = false,
                ModerationStatus = "PendingReview",
                SubmittedAt = DateTimeOffset.UtcNow,
                Message = "Tin đăng đã lưu trạng thái PendingReview."
            };

            return Task.FromResult(new CreateListingSubmitResult(response, isFromLiveApi));
        }
    }

    private static CreateListingViewModel CreateValidViewModel(ILandlordListingService? service = null)
    {
        var vm = new CreateListingViewModel(service);
        vm.Title = "Phòng khép kín ban công thoáng cách ICTU 350m";
        vm.Description = "Phòng mới xây sạch đẹp, có điều hòa, nóng lạnh, khóa vân tay 24/7 và chỗ để xe riêng.";
        vm.RentText = "1800000";
        vm.DepositText = "1800000";
        vm.AreaText = "22.5";
        vm.Address = "Ngõ 23 Đường Z115, Quyết Thắng, Thái Nguyên";
        vm.LandlordName = "Cô Lan";
        vm.LandlordPhone = "0987654321";
        vm.AddSamplePhotosCommand.Execute(null);
        return vm;
    }

    [Test]
    public void ValidateForm_EmptyRequiredFields_ReturnsFalseAndSetsFieldErrors()
    {
        var vm = new CreateListingViewModel();

        var isValid = vm.ValidateForm();

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(vm.HasTitleError, Is.True);
            Assert.That(vm.HasDescriptionError, Is.True);
            Assert.That(vm.HasRentError, Is.True);
            Assert.That(vm.HasAreaError, Is.True);
            Assert.That(vm.HasAddressError, Is.True);
            Assert.That(vm.HasLandlordNameError, Is.True);
            Assert.That(vm.HasLandlordPhoneError, Is.True);
            Assert.That(vm.HasPhotosError, Is.True);
            Assert.That(vm.HasFormErrorSummary, Is.True);
        });
    }

    [Test]
    public void TryAddPhoto_NonJpgOrPngFormat_RejectsFileAndSetsError()
    {
        var vm = new CreateListingViewModel();

        var (success, error) = vm.TryAddPhoto("phong_tro.gif", "image/gif", 500_000, "/tmp/phong_tro.gif");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("JPG hoặc PNG"));
            Assert.That(vm.Photos, Is.Empty);
            Assert.That(vm.HasPhotosError, Is.True);
        });
    }

    [Test]
    public void TryAddPhoto_ExceedsTenMegabytes_RejectsFileAndSetsError()
    {
        var vm = new CreateListingViewModel();
        long elevenMb = 11L * 1024L * 1024L;

        var (success, error) = vm.TryAddPhoto("anh_lon.jpg", "image/jpeg", elevenMb, "/tmp/anh_lon.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain("10 MB"));
            Assert.That(vm.Photos, Is.Empty);
            Assert.That(vm.HasPhotosError, Is.True);
        });
    }

    [Test]
    public void TryAddPhoto_ValidJpgAndPngWithinLimit_AddsAndRenumbersPhotos()
    {
        var vm = new CreateListingViewModel();

        var r1 = vm.TryAddPhoto("anh1.jpg", "image/jpeg", 1_200_000, "/tmp/anh1.jpg");
        var r2 = vm.TryAddPhoto("anh2.png", "image/png", 2_400_000, "/tmp/anh2.png", is360: true);
        var r3 = vm.TryAddPhoto("anh3.jpeg", "image/jpeg", 900_000, "/tmp/anh3.jpeg");

        Assert.Multiple(() =>
        {
            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r3.Success, Is.True);
            Assert.That(vm.Photos, Has.Count.EqualTo(3));
            Assert.That(vm.Photos[0].Order, Is.EqualTo(1));
            Assert.That(vm.Photos[1].Order, Is.EqualTo(2));
            Assert.That(vm.Photos[1].Is360, Is.True);
            Assert.That(vm.Photos[2].Order, Is.EqualTo(3));
            Assert.That(vm.PhotoCountSummaryText, Does.Contain("đạt tối thiểu 3 ảnh"));
        });
    }

    [Test]
    public async Task SubmitListingCommand_FewerThanThreePhotos_DoesNotSubmit()
    {
        var recorder = new RecordingLandlordListingService();
        var vm = CreateValidViewModel(recorder);
        vm.RemovePhotoCommand.Execute(vm.Photos[0]); // còn 2 ảnh

        await vm.SubmitListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(recorder.CallCount, Is.EqualTo(0));
            Assert.That(vm.HasPhotosError, Is.True);
            Assert.That(vm.PhotosError, Does.Contain("tối thiểu 3 ảnh"));
            Assert.That(vm.IsSubmittedPendingReview, Is.False);
        });
    }

    [Test]
    public async Task SubmitListingCommand_InvalidLandlordPhone_DoesNotSubmit()
    {
        var recorder = new RecordingLandlordListingService();
        var vm = CreateValidViewModel(recorder);
        vm.LandlordPhone = "12345";

        await vm.SubmitListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(recorder.CallCount, Is.EqualTo(0));
            Assert.That(vm.HasLandlordPhoneError, Is.True);
            Assert.That(vm.IsSubmittedPendingReview, Is.False);
        });
    }

    [Test]
    public void ToggleAmenityAndPhoto360AndRoomType_UpdateSelectionStates()
    {
        var vm = CreateValidViewModel();
        var airConditioner = vm.Amenities[3];
        var studioOption = vm.RoomTypeOptions[1];
        var firstPhoto = vm.Photos[0];

        vm.ToggleAmenityCommand.Execute(airConditioner);
        vm.SelectRoomTypeCommand.Execute(studioOption);
        vm.TogglePhoto360Command.Execute(firstPhoto);

        Assert.Multiple(() =>
        {
            Assert.That(airConditioner.IsSelected, Is.True);
            Assert.That(vm.SelectedRoomType, Is.EqualTo("Studio"));
            Assert.That(studioOption.IsSelected, Is.True);
            Assert.That(firstPhoto.Is360, Is.True);
        });
    }

    [Test]
    public async Task SubmitListingCommand_ValidForm_SubmitsAsPendingReviewUnverified()
    {
        var recorder = new RecordingLandlordListingService(isFromLiveApi: true);
        var vm = CreateValidViewModel(recorder);

        await vm.SubmitListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(recorder.CallCount, Is.EqualTo(1));
            Assert.That(recorder.LastRequest, Is.Not.Null);
            Assert.That(recorder.LastRequest!.Photos, Has.Count.EqualTo(3));
            Assert.That(recorder.LastRequest.Rent, Is.EqualTo(1_800_000m));
            Assert.That(recorder.LastRequest.Area, Is.EqualTo(22.5));
            Assert.That(vm.IsSubmittedPendingReview, Is.True);
            Assert.That(vm.SubmittedStatus, Is.EqualTo("PendingReview"));
            Assert.That(vm.SubmittedIsVerified, Is.False);
            Assert.That(vm.SubmittedListingId, Is.EqualTo("room-pending-2026-test"));
            Assert.That(vm.IsFromLiveApi, Is.True);
            Assert.That(vm.HasFormErrorSummary, Is.False);
        });
    }

    [Test]
    public async Task SubmitListingCommand_DefaultMockService_ReturnsPendingReviewOfflineResult()
    {
        var vm = CreateValidViewModel(new MockLandlordListingService());

        await vm.SubmitListingCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsSubmittedPendingReview, Is.True);
            Assert.That(vm.SubmittedStatus, Is.EqualTo("PendingReview"));
            Assert.That(vm.SubmittedIsVerified, Is.False);
            Assert.That(vm.SubmittedListingId, Does.StartWith("room-pending-"));
            Assert.That(vm.IsFromLiveApi, Is.False);
        });
    }

    [Test]
    public async Task ResetFormCommand_AfterSubmission_ClearsInputsPhotosAndPendingBadge()
    {
        var vm = CreateValidViewModel(new MockLandlordListingService());
        await vm.SubmitListingCommand.ExecuteAsync(null);

        vm.ResetFormCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsSubmittedPendingReview, Is.False);
            Assert.That(vm.SubmittedListingId, Is.Empty);
            Assert.That(vm.Title, Is.Empty);
            Assert.That(vm.RentText, Is.Empty);
            Assert.That(vm.Photos, Is.Empty);
            Assert.That(vm.HasFormErrorSummary, Is.False);
        });
    }
}
