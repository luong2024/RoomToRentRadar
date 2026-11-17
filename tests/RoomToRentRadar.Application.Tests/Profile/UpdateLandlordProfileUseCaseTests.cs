using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class UpdateLandlordProfileUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExecuteAsync_WhenProfileDoesNotExist_ShouldCallAddAsyncAndNotUpdateAsync()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateLandlordCommand("Chu Tro A", "0911223344", null, "Bio", "Khu Tro A", "Thai Nguyen", null, null, null, null, "VCB", "123456", "CHU TRO A");
        var result = await useCase.ExecuteAsync(userId, cmd);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("Chu Tro A"));
            Assert.That(repo.AddCallCount, Is.EqualTo(1));
            Assert.That(repo.UpdateCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenProfileAlreadyExists_ShouldCallUpdateAsyncAndNotAddAsync()
    {
        var userId = Guid.NewGuid();
        var existing = UserProfile.CreateDefault(userId, UserRole.Landlord, FixedNow);
        existing.SetVersion("\"landlord-etag-456\"");
        var repo = new TrackingUserProfileRepository(existing);
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateLandlordCommand("Chu Tro B", "0999888777", null, "Bio New", "Khu Tro B", "Thai Nguyen", null, null, null, null, "BIDV", "654321", "CHU TRO B");
        var result = await useCase.ExecuteAsync(userId, cmd);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("Chu Tro B"));
            Assert.That(repo.AddCallCount, Is.EqualTo(0));
            Assert.That(repo.UpdateCallCount, Is.EqualTo(1));
            Assert.That(repo.LastUpdatedProfile?.Version, Is.EqualTo("\"landlord-etag-456\""));
        });
    }

    [Test]
    public void ExecuteAsync_WhenAddAsyncEncountersConflict_ShouldThrowConcurrencyConflictException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository { ShouldThrowConflictOnAdd = true };
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateLandlordCommand("Chu Tro C", null, null, null, null, null, null, null, null, null, null, null, null);

        Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));
        Assert.That(repo.AddCallCount, Is.EqualTo(1));
        Assert.That(repo.UpdateCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_WhenSubmittingKycWithValidBlobNames_ShouldValidateDocumentsAndSetPending()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro D", "0911223344", null, "Bio", "Khu Tro D", "Thai Nguyen",
            "001200000001", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO D");

        var result = await useCase.ExecuteAsync(userId, cmd);

        Assert.Multiple(() =>
        {
            Assert.That(result.Landlord?.VerificationStatus, Is.EqualTo("Pending"));
            Assert.That(fakeStorage.PromotedBlobs, Does.Contain(frontBlob));
            Assert.That(fakeStorage.PromotedBlobs, Does.Contain(backBlob));
        });
    }

    [Test]
    public void ExecuteAsync_WhenSubmittingKycWithBlobBelongingToAnotherUser_ShouldThrowKycOperationInvalidException()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{otherUserId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro E", "0911223344", null, "Bio", "Khu Tro E", "Thai Nguyen",
            "001200000002", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO E");

        var ex = Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Invalid KYC document"));
    }

    [Test]
    public void ExecuteAsync_WhenFrontAndBackUseTheSameBlob_ShouldThrowKycOperationInvalidException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));
        var sharedBlob = $"{userId:D}/kyc/staging/front/shared.jpg";

        var cmd = new UpdateLandlordCommand("Chu Tro", "0911223344", null, null, null, null,
            "001200000003", new DateOnly(2021, 5, 20), sharedBlob, sharedBlob, null, null, null);

        Assert.ThrowsAsync<KycOperationInvalidException>(() => useCase.ExecuteAsync(userId, cmd));
        Assert.That(fakeStorage.PromotedBlobs, Is.Empty);
    }

    [Test]
    public void ExecuteAsync_WhenCitizenIdIssueDateIsInFuture_ShouldThrowKycOperationInvalidException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var futureDate = DateOnly.FromDateTime(FixedNow.DateTime.AddDays(10));
        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro F", "0911223344", null, "Bio", "Khu Tro F", "Thai Nguyen",
            "001200000004", futureDate, frontBlob, backBlob, "VCB", "123456", "CHU TRO F");

        var ex = Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Citizen ID issue date cannot be in the future"));
    }

    [Test]
    public void ExecuteAsync_WhenBackPromotionFails_ShouldCompensateByDeletingPromotedFrontBlob()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService { FailOnBackPromotion = true };
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro G", "0911223344", null, "Bio", "Khu Tro G", "Thai Nguyen",
            "001200000005", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO G");

        var ex = Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Simulated back promotion failure"));
        // Front blob was promoted, so compensation must delete the verified front blob
        var expectedVerifiedFrontBlob = frontBlob.Replace("/kyc/staging/", "/kyc/verified/");
        Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedFrontBlob));
    }

    [Test]
    public void ExecuteAsync_WhenRepositorySaveFails_ShouldCompensateByDeletingBothPromotedBlobs()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository { ShouldThrowConflictOnAdd = true };
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro H", "0911223344", null, "Bio", "Khu Tro H", "Thai Nguyen",
            "001200000006", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO H");

        Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        var expectedVerifiedFrontBlob = frontBlob.Replace("/kyc/staging/", "/kyc/verified/");
        var expectedVerifiedBackBlob = backBlob.Replace("/kyc/staging/", "/kyc/verified/");

        Assert.Multiple(() =>
        {
            Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedFrontBlob));
            Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedBackBlob));
        });
    }

    [Test]
    public void ExecuteAsync_WhenBackPromotionThrowsInfrastructureException_ShouldCompensateAndRethrowDirectlyFor500()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService
        {
            ExceptionToThrowOnBackPromotion = new InvalidDataException("Storage network partition error")
        };
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro I", "0911223344", null, "Bio", "Khu Tro I", "Thai Nguyen",
            "001200000007", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO I");

        // Infrastructure errors must NOT be masked as KycOperationInvalidException (HTTP 400); they must bubble up for HTTP 500
        var ex = Assert.ThrowsAsync<InvalidDataException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Storage network partition error"));
        var expectedVerifiedFrontBlob = frontBlob.Replace("/kyc/staging/", "/kyc/verified/");
        Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedFrontBlob), "Compensation rollback must still delete promoted blob");
    }

    [Test]
    public void ExecuteAsync_WhenStorageThrowsTimeoutException_ShouldCompensateAndRethrowDirectlyFor500()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService
        {
            ExceptionToThrowOnBackPromotion = new TimeoutException("Azure Storage request timed out")
        };
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro I", "0911223344", null, "Bio", "Khu Tro I", "Thai Nguyen",
            "001200000007", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO I");

        var ex = Assert.ThrowsAsync<TimeoutException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Azure Storage request timed out"));
        var expectedVerifiedFrontBlob = frontBlob.Replace("/kyc/staging/", "/kyc/verified/");
        Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedFrontBlob));
    }

    [Test]
    public void ExecuteAsync_WhenStorageThrowsWrappedNetworkException_ShouldCompensateAndRethrowDirectlyFor500()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService
        {
            ExceptionToThrowOnBackPromotion = new InvalidOperationException("Failed to access blob", new HttpRequestException("Connection reset by peer"))
        };
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro I", "0911223344", null, "Bio", "Khu Tro I", "Thai Nguyen",
            "001200000007", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO I");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.InnerException, Is.InstanceOf<HttpRequestException>());
        var expectedVerifiedFrontBlob = frontBlob.Replace("/kyc/staging/", "/kyc/verified/");
        Assert.That(fakeStorage.DeletedBlobs, Does.Contain(expectedVerifiedFrontBlob));
    }

    [Test]
    public void ExecuteAsync_WhenStorageThrowsBlobNotFound_ShouldWrapInKycOperationInvalidExceptionFor400()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService
        {
            ExceptionToThrowOnBackPromotion = new InvalidOperationException("The KYC document blob does not exist in storage.")
        };
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(FixedNow));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        var cmd = new UpdateLandlordCommand(
            "Chu Tro I", "0911223344", null, "Bio", "Khu Tro I", "Thai Nguyen",
            "001200000007", new DateOnly(2021, 5, 20),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO I");

        var ex = Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("The KYC document blob does not exist in storage."));
    }

    [Test]
    public async Task ExecuteAsync_WhenVietnamDateMatchesTodayInIctEvenIfPreviousDayInUtc_ShouldSucceed()
    {
        // 20:00 UTC on 2026-11-12 is 03:00 AM on 2026-11-13 in Vietnam (UTC+7)
        var earlyMorningVietnamUtc = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(earlyMorningVietnamUtc));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        // Issue date is 2026-11-13, which is today in Vietnam time (even though UTC is still 2026-11-12)
        var cmd = new UpdateLandlordCommand(
            "Chu Tro J", "0911223344", null, "Bio", "Khu Tro J", "Thai Nguyen",
            "001200000008", new DateOnly(2026, 11, 13),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO J");

        var result = await useCase.ExecuteAsync(userId, cmd);
        Assert.That(result.Landlord?.VerificationStatus, Is.EqualTo("Pending"));
    }

    [Test]
    public void ExecuteAsync_WhenVietnamDateIsInFutureInIct_ShouldThrowKycOperationInvalidException()
    {
        // 20:00 UTC on 2026-11-12 is 03:00 AM on 2026-11-13 in Vietnam (UTC+7)
        var earlyMorningVietnamUtc = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var fakeStorage = new FakeKycDocumentStorageService();
        var useCase = new UpdateLandlordProfileUseCase(repo, fakeStorage, new FixedTimeProvider(earlyMorningVietnamUtc));

        var frontBlob = $"{userId:D}/kyc/staging/front/front.jpg";
        var backBlob = $"{userId:D}/kyc/staging/back/back.jpg";

        // 2026-11-14 is tomorrow in Vietnam time
        var cmd = new UpdateLandlordCommand(
            "Chu Tro K", "0911223344", null, "Bio", "Khu Tro K", "Thai Nguyen",
            "001200000009", new DateOnly(2026, 11, 14),
            frontBlob, backBlob, "VCB", "123456", "CHU TRO K");

        var ex = Assert.ThrowsAsync<KycOperationInvalidException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Citizen ID issue date cannot be in the future"));
    }
}

internal sealed class FakeKycDocumentStorageService : IKycDocumentStorageService
{
    public List<string> PromotedBlobs { get; } = new();
    public List<string> DeletedBlobs { get; } = new();
    public bool FailOnBackPromotion { get; set; }
    public Exception? ExceptionToThrowOnBackPromotion { get; set; }

    public Task<string?> GeneratePresignedReadUrlAsync(string? rawBlobPath, TimeSpan validDuration, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(rawBlobPath is null ? null : $"https://fake.storage/read/{rawBlobPath}?sig=valid");

    public Task<KycUploadUrlResponse> GeneratePresignedUploadUrlAsync(Guid userId, string documentSide, string contentType, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KycUploadUrlResponse($"{userId:D}/kyc/staging/{documentSide}/{Guid.NewGuid():N}.jpg", $"https://fake.storage/upload?sig=valid", DateTimeOffset.UtcNow.AddMinutes(10)));

    public Task ValidateUploadedDocumentAsync(Guid userId, string blobName, CancellationToken cancellationToken = default)
    {
        var expectedPrefix = $"{userId:D}/kyc/staging/";
        if (!blobName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The KYC document does not belong to the current user.");
        }
        return Task.CompletedTask;
    }

    public async Task<string> ValidateAndPromoteUploadedDocumentAsync(Guid userId, string documentSide, string blobName, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrowOnBackPromotion is not null && string.Equals(documentSide, "back", StringComparison.OrdinalIgnoreCase))
        {
            throw ExceptionToThrowOnBackPromotion;
        }

        if (FailOnBackPromotion && string.Equals(documentSide, "back", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated back promotion failure");
        }

        var expectedPrefix = $"{userId:D}/kyc/staging/{documentSide}/";
        if (!blobName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The KYC document is not for the expected document side.");

        await ValidateUploadedDocumentAsync(userId, blobName, cancellationToken);
        PromotedBlobs.Add(blobName);
        return blobName.Replace("/kyc/staging/", "/kyc/verified/", StringComparison.OrdinalIgnoreCase);
    }

    public Task DeleteBlobIfExistsAsync(string? blobName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(blobName))
        {
            DeletedBlobs.Add(blobName);
        }
        return Task.CompletedTask;
    }

    public void ValidateDocumentUrl(string? rawBlobPath) { }

    public bool IsAuthorizedDocumentUrl(string? rawBlobPath) => true;
}
