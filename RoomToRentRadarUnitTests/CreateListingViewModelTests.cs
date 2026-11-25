using Moq;
using NUnit.Framework;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.RoomListings;
using RoomToRentRadar.ViewModels.RoomListings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class CreateListingViewModelTests
{
    private Mock<ICreateListingService> _mockCreateListingService = null!;
    private Mock<IMediaPickerService> _mockMediaPickerService = null!;
    private CreateListingViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _mockCreateListingService = new Mock<ICreateListingService>();
        _mockMediaPickerService = new Mock<IMediaPickerService>();

        _viewModel = new CreateListingViewModel(
            _mockCreateListingService.Object,
            _mockMediaPickerService.Object);
    }

    [Test]
    public void Constructor_InitializesDefaults()
    {
        Assert.That(_viewModel.RoomTypeOptions, Is.Not.Null);
        Assert.That(_viewModel.RoomTypeOptions.Count, Is.EqualTo(4));
        Assert.That(_viewModel.Photos, Is.Empty);
        Assert.That(_viewModel.IsSubmitting, Is.False);
        Assert.That(_viewModel.HasError, Is.False);
        Assert.That(_viewModel.IsSuccess, Is.False);
    }

    #region Title Validation Tests

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ValidateForm_EmptyTitle_SetsTitleError(string? title)
    {
        _viewModel.Title = title!;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.TitleError, Is.Not.Null);
        Assert.That(_viewModel.TitleError, Does.Contain("nhập tiêu đề"));
    }

    [Test]
    public void ValidateForm_TitleTooShort_SetsTitleError()
    {
        _viewModel.Title = "Ngắn quá"; // 8 chars
        _viewModel.ValidateForm();

        Assert.That(_viewModel.TitleError, Is.Not.Null);
        Assert.That(_viewModel.TitleError, Does.Contain("từ 10 đến 100 ký tự"));
    }

    [Test]
    public void ValidateForm_TitleTooLong_SetsTitleError()
    {
        _viewModel.Title = new string('A', 101);
        _viewModel.ValidateForm();

        Assert.That(_viewModel.TitleError, Is.Not.Null);
        Assert.That(_viewModel.TitleError, Does.Contain("từ 10 đến 100 ký tự"));
    }

    [Test]
    public void ValidateForm_ValidTitle_HasNoTitleError()
    {
        _viewModel.Title = "Phòng trọ sinh viên khép kín gần ICTU";
        _viewModel.ValidateForm();

        Assert.That(_viewModel.TitleError, Is.Null);
    }

    #endregion

    #region Room Type Validation Tests

    [Test]
    public void ValidateForm_NoRoomType_SetsRoomTypeError()
    {
        _viewModel.SelectedRoomType = null;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.RoomTypeError, Is.Not.Null);
        Assert.That(_viewModel.RoomTypeError, Does.Contain("chọn loại hình"));
    }

    [Test]
    public void ValidateForm_InvalidRoomType_SetsRoomTypeError()
    {
        _viewModel.SelectedRoomType = "PenthouseLuxury";
        _viewModel.ValidateForm();

        Assert.That(_viewModel.RoomTypeError, Is.Not.Null);
        Assert.That(_viewModel.RoomTypeError, Does.Contain("không hợp lệ"));
    }

    [TestCase("BoardingRoom")]
    [TestCase("MiniApartment")]
    [TestCase("WholeHouse")]
    [TestCase("Dormitory")]
    public void ValidateForm_ValidRoomType_HasNoRoomTypeError(string roomType)
    {
        _viewModel.SelectRoomType(roomType);
        _viewModel.ValidateForm();

        Assert.That(_viewModel.RoomTypeError, Is.Null);
        Assert.That(_viewModel.SelectedRoomType, Is.EqualTo(roomType));
    }

    #endregion

    #region Area Validation Tests

    [TestCase("")]
    [TestCase("   ")]
    public void ValidateForm_EmptyArea_SetsAreaError(string areaText)
    {
        _viewModel.AreaText = areaText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AreaError, Is.Not.Null);
        Assert.That(_viewModel.AreaError, Does.Contain("nhập diện tích"));
    }

    [TestCase("abc")]
    [TestCase("twenty")]
    [TestCase("NaN")]
    [TestCase("nan")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void ValidateForm_InvalidAreaFormat_SetsAreaError(string areaText)
    {
        _viewModel.AreaText = areaText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AreaError, Does.Contain("định dạng số").Or.Contain("từ 5 m² đến 200 m²"));
    }

    [TestCase("4.9")]
    [TestCase("0")]
    [TestCase("-10")]
    public void ValidateForm_AreaLessThan5_SetsAreaError(string areaText)
    {
        _viewModel.AreaText = areaText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AreaError, Does.Contain("từ 5 m² đến 200 m²"));
    }

    [TestCase("200.1")]
    [TestCase("350")]
    public void ValidateForm_AreaGreaterThan200_SetsAreaError(string areaText)
    {
        _viewModel.AreaText = areaText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AreaError, Does.Contain("từ 5 m² đến 200 m²"));
    }

    [TestCase("25")]
    [TestCase("25.5")]
    [TestCase("25,5")]
    [TestCase("5")]
    [TestCase("200")]
    public void ValidateForm_ValidArea_HasNoAreaError(string areaText)
    {
        _viewModel.AreaText = areaText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AreaError, Is.Null);
    }

    #endregion

    #region Address Validation Tests

    [TestCase("")]
    [TestCase("   ")]
    public void ValidateForm_EmptyAddress_SetsAddressError(string address)
    {
        _viewModel.Address = address;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AddressError, Does.Contain("nhập địa chỉ"));
    }

    [Test]
    public void ValidateForm_AddressTooLong_SetsAddressError()
    {
        _viewModel.Address = new string('X', 201);
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AddressError, Does.Contain("vượt quá 200 ký tự"));
    }

    [Test]
    public void ValidateForm_ValidAddress_HasNoAddressError()
    {
        _viewModel.Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên";
        _viewModel.ValidateForm();

        Assert.That(_viewModel.AddressError, Is.Null);
    }

    #endregion

    #region Price & Deposit Validation Tests

    [TestCase("")]
    [TestCase("abc")]
    public void ValidateForm_EmptyOrInvalidPrice_SetsPriceError(string priceText)
    {
        _viewModel.PriceText = priceText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.PriceError, Is.Not.Null);
    }

    [TestCase("99999")]
    [TestCase("50000001")]
    public void ValidateForm_PriceOutOfRange_SetsPriceError(string priceText)
    {
        _viewModel.PriceText = priceText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.PriceError, Does.Contain("từ 100.000 ₫ đến 50.000.000 ₫"));
    }

    [TestCase("1500000")]
    [TestCase("1.500.000")]
    [TestCase("1,500,000")]
    public void ValidateForm_ValidPrice_HasNoPriceError(string priceText)
    {
        _viewModel.PriceText = priceText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.PriceError, Is.Null);
        Assert.That(_viewModel.FormattedPricePreview, Does.Contain("1.500.000"));
    }

    [TestCase("-1000")]
    public void ValidateForm_DepositNegative_SetsDepositError(string depositText)
    {
        _viewModel.PriceText = "2000000";
        _viewModel.DepositText = depositText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.DepositError, Does.Contain("không được là số âm"));
    }

    [TestCase("0")]
    public void ValidateForm_DepositZero_HasNoDepositError(string depositText)
    {
        _viewModel.PriceText = "2000000";
        _viewModel.DepositText = depositText;
        _viewModel.ValidateForm();

        Assert.That(_viewModel.DepositError, Is.Null);
        Assert.That(_viewModel.FormattedDepositPreview, Does.Contain("Không yêu cầu"));
    }

    [Test]
    public void ValidateForm_DepositExceedsThreeTimesRent_SetsDepositError()
    {
        _viewModel.PriceText = "2000000";
        _viewModel.DepositText = "6000001"; // > 3 * 2,000,000
        _viewModel.ValidateForm();

        Assert.That(_viewModel.DepositError, Does.Contain("không được vượt quá 3 lần tiền thuê"));
    }

    [Test]
    public void ValidateForm_DepositExactlyThreeTimesRent_HasNoDepositError()
    {
        _viewModel.PriceText = "2000000";
        _viewModel.DepositText = "6000000"; // = 3 * 2,000,000
        _viewModel.ValidateForm();

        Assert.That(_viewModel.DepositError, Is.Null);
    }

    #endregion

    #region Photos Validation Tests

    [Test]
    public void ValidateForm_LessThan3Photos_SetsPhotosError()
    {
        AddTestPhotos(2);

        _viewModel.ValidateForm();

        Assert.That(_viewModel.PhotosError, Does.Contain("ít nhất 3 hình ảnh"));
        Assert.That(_viewModel.HasMinimumPhotos, Is.False);
    }

    [Test]
    public void ValidateForm_AtLeast3Photos_HasNoPhotosError()
    {
        AddTestPhotos(3);

        _viewModel.ValidateForm();

        Assert.That(_viewModel.PhotosError, Is.Null);
        Assert.That(_viewModel.HasMinimumPhotos, Is.True);
        Assert.That(_viewModel.Photos[0].IsCover, Is.True);
    }

    [Test]
    public void ValidateForm_OversizedPhoto_SetsPhotosError()
    {
        AddTestPhotos(3);
        _viewModel.Photos.Add(new ListingPhotoItem
        {
            FilePath = "C:/photos/huge.jpg",
            FileName = "huge.jpg",
            FileSizeBytes = CreateListingViewModel.MaxPhotoSizeBytes + 1024
        });

        _viewModel.ValidateForm();

        Assert.That(_viewModel.PhotosError, Does.Contain("vượt quá dung lượng"));
    }

    [Test]
    public async Task PickPhotosCommand_FiltersInvalidExtension()
    {
        var picked = new List<ListingPhotoItem>
        {
            new() { FilePath = "C:/doc.pdf", FileName = "doc.pdf", FileSizeBytes = 50000 },
            new() { FilePath = "C:/valid.jpg", FileName = "valid.jpg", FileSizeBytes = 50000 }
        };

        _mockMediaPickerService
            .Setup(s => s.PickPhotosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(picked);

        await _viewModel.PickPhotosCommand.ExecuteAsync(null);

        Assert.That(_viewModel.Photos.Count, Is.EqualTo(1));
        Assert.That(_viewModel.Photos[0].FileName, Is.EqualTo("valid.jpg"));
    }

    [Test]
    public void RemovePhotoCommand_RemovesPhotoAndUpdatesCover()
    {
        AddTestPhotos(3);
        var firstPhoto = _viewModel.Photos[0];
        var secondPhoto = _viewModel.Photos[1];

        _viewModel.RemovePhotoCommand.Execute(firstPhoto);

        Assert.That(_viewModel.Photos.Count, Is.EqualTo(2));
        Assert.That(_viewModel.Photos[0], Is.EqualTo(secondPhoto));
        Assert.That(_viewModel.Photos[0].IsCover, Is.True);
        Assert.That(_viewModel.PhotosError, Does.Contain("ít nhất 3 hình ảnh"));
    }

    #endregion

    #region Full Form Submit Tests

    [Test]
    public async Task SubmitListing_WhenFormInvalid_DoesNotCallService()
    {
        // Missing required fields
        _viewModel.Title = "";

        await _viewModel.SubmitListingCommand.ExecuteAsync(null);

        Assert.That(_viewModel.HasError, Is.True);
        Assert.That(_viewModel.ErrorMessage, Does.Contain("kiểm tra lại các trường"));
        _mockCreateListingService.Verify(s => s.CreateListingAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SubmitListing_WhenValid_CallsServiceAndSetsSuccess()
    {
        FillValidFormData();

        _mockCreateListingService
            .Setup(s => s.CreateListingAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateListingResult.Success("listing-123"));

        await _viewModel.SubmitListingCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_viewModel.SuccessMessage, Does.Contain("thành công"));
        Assert.That(_viewModel.HasError, Is.False);
        _mockCreateListingService.Verify(s => s.CreateListingAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SubmitListing_WhenServiceFails_SetsErrorMessage()
    {
        FillValidFormData();

        _mockCreateListingService
            .Setup(s => s.CreateListingAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateListingResult.Failure("Máy chủ quá tải, vui lòng thử lại sau."));

        await _viewModel.SubmitListingCommand.ExecuteAsync(null);

        Assert.That(_viewModel.IsSuccess, Is.False);
        Assert.That(_viewModel.HasError, Is.True);
        Assert.That(_viewModel.ErrorMessage, Does.Contain("Máy chủ quá tải"));
    }

    [Test]
    public void SubmitListing_WhenCancelled_RethrowsOperationCanceledException()
    {
        FillValidFormData();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCreateListingService
            .Setup(s => s.CreateListingAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _viewModel.SubmitListingAsync(cts.Token));

        Assert.That(_viewModel.IsSubmitting, Is.False);
    }

    #endregion

    [Test]
    public async Task GoBackAsync_WhenNavigationBackHandlerProvided_InvokesHandler()
    {
        var invoked = false;
        _viewModel.NavigationBackHandler = () =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        await _viewModel.GoBackCommand.ExecuteAsync(null);

        Assert.That(invoked, Is.True);
    }

    private void FillValidFormData()
    {
        _viewModel.Title = "Phòng trọ sinh viên khép kín gần cổng ICTU";
        _viewModel.SelectRoomType("BoardingRoom");
        _viewModel.AreaText = "25";
        _viewModel.Address = "Ngõ 12 Đường Z115, Xã Quyết Thắng, TP. Thái Nguyên";
        _viewModel.PriceText = "1500000";
        _viewModel.DepositText = "1000000";
        _viewModel.Description = "Phòng đầy đủ nóng lạnh, điều hòa, wifi tốc độ cao.";
        AddTestPhotos(3);
    }

    private void AddTestPhotos(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            _viewModel.Photos.Add(new ListingPhotoItem
            {
                FilePath = $"C:/photos/photo_{i}.jpg",
                FileName = $"photo_{i}.jpg",
                FileSizeBytes = 1024 * 1024 // 1 MB
            });
        }
    }
}
