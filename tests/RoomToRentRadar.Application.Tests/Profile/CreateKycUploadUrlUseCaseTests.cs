using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class CreateKycUploadUrlUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_WithValidFrontJpeg_ShouldReturnUploadUrl()
    {
        var userId = Guid.NewGuid();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new CreateKycUploadUrlUseCase(fakeStorage);

        var req = new CreateKycUploadUrlRequest("front", "image/jpeg");
        var result = await useCase.ExecuteAsync(userId, req);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.BlobName, Does.StartWith($"{userId:D}/kyc/"));
            Assert.That(result.UploadUrl, Does.Contain("fake.storage/upload"));
        });
    }

    [Test]
    public void ExecuteAsync_WithInvalidDocumentSide_ShouldThrowKycOperationInvalidException()
    {
        var userId = Guid.NewGuid();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new CreateKycUploadUrlUseCase(fakeStorage);

        var req = new CreateKycUploadUrlRequest("side_view", "image/jpeg");
        Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, req));
    }

    [Test]
    public void ExecuteAsync_WithInvalidContentType_ShouldThrowKycOperationInvalidException()
    {
        var userId = Guid.NewGuid();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new CreateKycUploadUrlUseCase(fakeStorage);

        var req = new CreateKycUploadUrlRequest("front", "application/pdf");
        Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, req));
    }
}
