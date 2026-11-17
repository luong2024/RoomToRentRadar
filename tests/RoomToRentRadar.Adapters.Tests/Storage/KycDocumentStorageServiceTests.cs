using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Adapters.Outbound.Storage;

namespace RoomToRentRadar.Adapters.Tests.Storage;

[TestFixture]
public class KycDocumentStorageServiceTests
{
    private static readonly string ValidTestAccountKey =
        Convert.ToBase64String(Encoding.UTF8.GetBytes("super-secret-azure-storage-key-32-bytes!"));

    [Test]
    public void Constructor_InProduction_WhenConfigurationMissing_ShouldThrowInvalidOperationException()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var emptyConfig = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new KycDocumentStorageService(emptyConfig, prodEnv));

        Assert.That(ex.Message, Does.Contain("Production").And.Contain("credentials"));
    }

    [Test]
    public void Constructor_InProduction_WhenUsingAzuriteDefaultCredentials_ShouldThrowInvalidOperationException()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = KycDocumentStorageService.DefaultAzuriteAccountName,
                ["KycStorage:AccountKey"] = KycDocumentStorageService.DefaultAzuriteAccountKey,
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new KycDocumentStorageService(config, prodEnv));

        Assert.That(ex.Message, Does.Contain("Default Azurite credentials cannot be used in a Production environment"));
    }

    [Test]
    public void Constructor_InProduction_WhenUsingDevelopmentStorageConnectionString_ShouldThrowInvalidOperationException()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureStorage:ConnectionString"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new KycDocumentStorageService(config, prodEnv));

        Assert.That(ex.Message, Does.Contain("UseDevelopmentStorage=true").And.Contain("Production"));
    }

    [Test]
    public void Constructor_InDevelopment_WhenConfigurationMissing_ShouldFallbackToAzuriteCredentials()
    {
        var devEnv = new TestHostEnvironment("Development");
        var emptyConfig = new ConfigurationBuilder().Build();

        Assert.DoesNotThrow(() => new KycDocumentStorageService(emptyConfig, devEnv));
    }

    [Test]
    public void Constructor_InProduction_WhenBlobEndpointUsesHttp_ShouldThrowInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "radarkycprod",
                ["KycStorage:AccountKey"] = ValidTestAccountKey,
                ["KycStorage:BlobEndpoint"] = "http://radarkycprod.blob.core.windows.net"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new KycDocumentStorageService(config, new TestHostEnvironment("Production")));

        Assert.That(ex!.Message, Does.Contain("BlobEndpoint").And.Contain("HTTPS"));
    }

    /// <summary>
    /// Verifies that cryptographic Shared Access Signature (SAS) tokens generated for an account
    /// produce valid query parameter structure, read permissions ('r'), blob resource ('b'),
    /// and are accurately parsed by Azure.Storage.Blobs.BlobClient without making live outbound HTTP requests.
    /// </summary>
    [Test]
    public async Task GeneratePresignedReadUrlAsync_WithConfiguredAccount_GeneratesValidSasUrlStructure_VerifiableByAzureBlobClient()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "radarkycprod",
                ["KycStorage:AccountKey"] = ValidTestAccountKey,
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, prodEnv);
        var blobUrl = "https://radarkycprod.blob.core.windows.net/kyc/landlord_id_front.jpg";

        var sasUrl = await service.GeneratePresignedReadUrlAsync(blobUrl, TimeSpan.FromMinutes(20));

        Assert.That(sasUrl, Is.Not.Null);
        Assert.That(sasUrl, Does.StartWith("https://radarkycprod.blob.core.windows.net/kyc/landlord_id_front.jpg?"));

        // Prove that the SAS URL is fully compatible with Azure Storage BlobClient
        var blobClient = new BlobClient(new Uri(sasUrl!));
        Assert.Multiple(() =>
        {
            Assert.That(blobClient.AccountName, Is.EqualTo("radarkycprod"));
            Assert.That(blobClient.BlobContainerName, Is.EqualTo("kyc"));
            Assert.That(blobClient.Name, Is.EqualTo("landlord_id_front.jpg"));
        });

        // Parse SAS parameters to verify read permissions and resource format
        var blobUriBuilder = new BlobUriBuilder(new Uri(sasUrl!));
        Assert.Multiple(() =>
        {
            Assert.That(blobUriBuilder.Sas.Resource, Is.EqualTo("b"));
            Assert.That(blobUriBuilder.Sas.Permissions, Is.EqualTo("r"));
            Assert.That(blobUriBuilder.Sas.Protocol, Is.EqualTo(SasProtocol.Https));
            Assert.That(blobUriBuilder.Sas.Signature, Is.Not.Empty);
            Assert.That(blobUriBuilder.Sas.ExpiresOn, Is.GreaterThan(DateTimeOffset.UtcNow));
        });
    }

    [Test]
    public void ValidateDocumentUrl_WhenContainerIsMaliciousStartsWithPrefix_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, devEnv);
        var maliciousUrl = "https://devstoreaccount1.blob.core.windows.net/kyc-malicious/fake_doc.jpg";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ValidateDocumentUrl(maliciousUrl));

        Assert.That(ex.Message, Does.Contain("Blob container 'kyc-malicious' is not authorized"));
    }

    [Test]
    public void ValidateDocumentUrl_WhenHostIsUnauthorized_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, devEnv);
        var attackerUrl = "https://attacker.evil.com/kyc/stolen_id.jpg";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ValidateDocumentUrl(attackerUrl));

        Assert.That(ex.Message, Does.Contain("Blob host 'attacker.evil.com' is not authorized"));
    }

    [Test]
    public void ValidateDocumentUrl_WhenAccountDoesNotMatch_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "devstoreaccount1",
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, devEnv);
        // Standard Azure path contains foreign account name
        var mismatchUrl = "https://foreignaccount.blob.core.windows.net/kyc/doc.jpg";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ValidateDocumentUrl(mismatchUrl));

        Assert.That(ex.Message, Does.Contain("not authorized"));
    }

    [Test]
    public void ValidateDocumentUrl_InProduction_WhenHttpSchemeUsed_ShouldThrowInvalidOperationException()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "radarkycprod",
                ["KycStorage:AccountKey"] = ValidTestAccountKey,
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, prodEnv);
        var insecureUrl = "http://radarkycprod.blob.core.windows.net/kyc/doc.jpg";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ValidateDocumentUrl(insecureUrl));

        Assert.That(ex.Message, Does.Contain("HTTPS scheme in Production"));
    }

    [Test]
    public void ValidateDocumentUrl_WhenValidHttpsAzureStorageBlob_ShouldSucceed()
    {
        var prodEnv = new TestHostEnvironment("Production");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "radarkycprod",
                ["KycStorage:AccountKey"] = ValidTestAccountKey,
                ["KycStorage:ContainerName"] = "kyc"
            })
            .Build();

        var service = new KycDocumentStorageService(config, prodEnv);
        var validUrl = "https://radarkycprod.blob.core.windows.net/kyc/doc.jpg";

        Assert.DoesNotThrow(() => service.ValidateDocumentUrl(validUrl));
        Assert.That(service.IsAuthorizedDocumentUrl(validUrl), Is.True);
    }

    [Test]
    public async Task GeneratePresignedUploadUrlAsync_GeneratesCreateOnlySasForSideSpecificStagingBlob()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var result = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.BlobName, Does.StartWith($"staging/{userId:D}/front/"));
            Assert.That(result.BlobName, Does.EndWith(".jpg"));

            var blobUriBuilder = new BlobUriBuilder(new Uri(result.UploadUrl));
            Assert.That(blobUriBuilder.Sas.Permissions, Is.EqualTo("c"));
            Assert.That(blobUriBuilder.Sas.Permissions, Does.Not.Contain("w"), "Upload SAS must never overwrite a blob");
            Assert.That(blobUriBuilder.Sas.Permissions, Does.Not.Contain("r"), "Upload SAS must NEVER grant Read permission");
            Assert.That(blobUriBuilder.Sas.Resource, Is.EqualTo("b"), "Upload SAS must be restricted to a single blob, not entire container");
            Assert.That(blobUriBuilder.Sas.Protocol, Is.EqualTo(SasProtocol.HttpsAndHttp), "In development with HTTP endpoint, HttpsAndHttp is required for Azurite compatibility");
        });
    }

    [Test]
    public async Task GeneratePresignedUploadUrlAsync_WithHttpsBlobEndpoint_GeneratesStrictHttpsSas()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KycStorage:AccountName"] = "radarstorage",
                ["KycStorage:AccountKey"] = ValidTestAccountKey,
                ["KycStorage:BlobEndpoint"] = "https://radarstorage.blob.core.windows.net/"
            })
            .Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var result = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        var blobUriBuilder = new BlobUriBuilder(new Uri(result.UploadUrl));
        Assert.That(blobUriBuilder.Sas.Protocol, Is.EqualTo(SasProtocol.Https));
    }

    [Test]
    public async Task GeneratePresignedUploadUrlAsync_ExpiresWithinExpectedTime()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var result = await service.GeneratePresignedUploadUrlAsync(userId, "back", "image/png");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExpiresAt, Is.GreaterThan(DateTimeOffset.UtcNow));
            Assert.That(result.ExpiresAt - DateTimeOffset.UtcNow, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(11)));
        });
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenBlobBelongsToUserAndValid_ShouldPass()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
        service.RegisterSimulatedBlob(upload.BlobName, "image/jpeg", 1024 * 1024);

        Assert.DoesNotThrowAsync(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenUploadUrlWasRequestedButBlobWasNotUploaded_ShouldThrowInvalidOperationException()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenBlobBelongsToAnotherUser_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();
        var victimUserId = Guid.NewGuid();

        var victimBlob = $"{victimUserId:D}/kyc/staging/front/front.jpg";

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, victimBlob));

        Assert.That(ex!.Message, Does.Contain("does not belong to the current user"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenBlobDoesNotExist_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var nonExistentBlob = $"{userId:D}/kyc/staging/front/ghost_photo.jpg";

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, nonExistentBlob));

        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenContentTypeIsInvalid_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var pdfBlob = $"{userId:D}/kyc/staging/front/document.jpg";
        service.RegisterSimulatedBlob(pdfBlob, contentType: "application/pdf", contentLength: 1024 * 1024);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, pdfBlob));

        Assert.That(ex!.Message, Does.Contain("Only image/jpeg and image/png are permitted"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenFileSizeExceedsLimit_ShouldThrowInvalidOperationException()
    {
        var devEnv = new TestHostEnvironment("Development");
        var config = new ConfigurationBuilder().Build();
        var service = new KycDocumentStorageService(config, devEnv);
        var userId = Guid.NewGuid();

        var oversizeBlob = $"{userId:D}/kyc/staging/front/huge_photo.jpg";
        const long sixMegabytes = 6 * 1024 * 1024;
        service.RegisterSimulatedBlob(oversizeBlob, contentType: "image/jpeg", contentLength: sixMegabytes);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, oversizeBlob));

        Assert.That(ex!.Message, Does.Contain("exceeds the maximum allowed limit of 5 MB"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenContentIsNotAnImageDespiteImageContentType_ShouldThrowInvalidOperationException()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var blobName = $"{userId:D}/kyc/staging/front/not-an-image.jpg";
        service.RegisterSimulatedBlob(blobName, "image/jpeg", 10, Encoding.UTF8.GetBytes("not image"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateUploadedDocumentAsync(userId, blobName));

        Assert.That(ex!.Message, Does.Contain("not a valid JPEG or PNG"));
    }

    [Test]
    public async Task ValidateAndPromoteUploadedDocumentAsync_MovesStagingBlobToVerifiedSide()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "back", "image/png");
        service.RegisterSimulatedBlob(upload.BlobName, "image/png", 1024);

        var verifiedBlob = await service.ValidateAndPromoteUploadedDocumentAsync(userId, "back", upload.BlobName);

        Assert.That(verifiedBlob, Does.StartWith($"verified/{userId:D}/back/"));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateUploadedDocumentAsync(userId, upload.BlobName));
    }

    [Test]
    public async Task ValidateAndPromoteUploadedDocumentAsync_WhenCopyFails_ShouldThrowAndPreserveStagingBlob()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
        service.RegisterSimulatedBlob(upload.BlobName, "image/jpeg");
        service.SimulateCopyFailure = true;

        var ex = Assert.ThrowsAsync<RequestFailedException>(async () =>
            await service.ValidateAndPromoteUploadedDocumentAsync(userId, "front", upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("Copy status: 'Failed'").And.Contain("Staging document was preserved"));

        // Staging document must NOT have been deleted
        Assert.DoesNotThrowAsync(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenFileSizeExceedsLimit_CleansUpOversizedBlobFromStaging()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
        const long sevenMegabytes = 7 * 1024 * 1024;
        service.RegisterSimulatedBlob(upload.BlobName, contentType: "image/jpeg", contentLength: sevenMegabytes);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("exceeds the maximum allowed limit of 5 MB"));

        // Oversized blob must have been cleaned up from storage
        var exNotFound = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(exNotFound!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public async Task GeneratePresignedUploadUrlAsync_WhenUserExceedsRateLimit_ShouldThrowRateLimitExceededException()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
        }

        var ex = Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RateLimitExceededException>(async () =>
            await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg"));

        Assert.That(ex!.Message, Does.Contain("Rate limit exceeded"));
    }

    [Test]
    public void GeneratePresignedUploadUrlAsync_ConcurrentUploadRequests_EnforcesAtomicRateLimiting()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, 15)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
                    return true;
                }
                catch (RoomToRentRadar.Application.Exceptions.RateLimitExceededException)
                {
                    return false;
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        var successCount = tasks.Count(t => t.Result);
        var rateLimitedCount = tasks.Count(t => !t.Result);

        Assert.Multiple(() =>
        {
            Assert.That(successCount, Is.EqualTo(10), "Exactly 10 requests should succeed within the rate limit window");
            Assert.That(rateLimitedCount, Is.EqualTo(5), "Excess requests should be rate limited atomically");
        });
    }

    [Test]
    public async Task InMemoryKycUploadRateLimiter_WhenWindowExpires_EvictsExpiredBuckets()
    {
        var startTime = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(startTime);
        var limiter = new InMemoryKycUploadRateLimiter(timeProvider, maxRequestsPerWindow: 5, window: TimeSpan.FromMinutes(5));

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await limiter.CheckAndAcquireAsync(user1);
        Assert.That(limiter.ActiveBucketCount, Is.EqualTo(1));

        // Advance beyond window (6 minutes later)
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Acquiring for user2 triggers eviction sweep of user1's expired bucket
        await limiter.CheckAndAcquireAsync(user2);

        // user1 was evicted, only user2 remains
        Assert.That(limiter.ActiveBucketCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GeneratePresignedUploadUrlAsync_CleansUpPreviousStagingBlobsForSameUserAndSide()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();

        var firstUpload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");
        service.RegisterSimulatedBlob(firstUpload.BlobName, "image/jpeg");

        // Staging blob exists
        Assert.DoesNotThrowAsync(async () =>
            await service.ValidateUploadedDocumentAsync(userId, firstUpload.BlobName));

        // Generate a second upload URL for same side
        var secondUpload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        // First staging blob should be cleaned up
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, firstUpload.BlobName));

        Assert.That(ex!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenPngHasValidHeaderButTruncatedChunks_ShouldThrowAndCleanUpStagingBlob()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/png");

        // Valid 8-byte signature + 25-byte IHDR (truncated, missing IDAT and IEND)
        var validMinimalPng = KycDocumentStorageService.CreateMinimalImage("image/png");
        var truncatedPng = validMinimalPng[..33]; // Cut off right after IHDR

        service.RegisterSimulatedBlob(upload.BlobName, "image/png", contentLength: truncatedPng.Length, content: truncatedPng);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("PNG").And.Contain("missing image data").Or.Contain("truncated"));

        // Malformed staging blob should be removed from storage
        var exNotFound = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(exNotFound!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenPngHasCorruptedCrc_ShouldThrowAndCleanUpStagingBlob()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "back", "image/png");

        var corruptPng = KycDocumentStorageService.CreateMinimalImage("image/png");
        // Corrupt a byte in the IDAT chunk data
        corruptPng[45] ^= 0xFF;

        service.RegisterSimulatedBlob(upload.BlobName, "image/png", contentLength: corruptPng.Length, content: corruptPng);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("CRC checksum verification failed").Or.Contain("corrupt"));

        // Cleaned up from staging
        var exNotFound = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(exNotFound!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenJpegHasValidHeaderButTruncatedBeforeEoi_ShouldThrowAndCleanUpStagingBlob()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        var validMinimalJpeg = KycDocumentStorageService.CreateMinimalImage("image/jpeg");
        // Truncate before EOI marker (remove last 2 bytes 0xFF, 0xD9)
        var truncatedJpeg = validMinimalJpeg[..^2];

        service.RegisterSimulatedBlob(upload.BlobName, "image/jpeg", contentLength: truncatedJpeg.Length, content: truncatedJpeg);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("JPEG").And.Contain("missing End Of Image (EOI) marker").Or.Contain("truncated"));

        // Cleaned up from staging
        var exNotFound = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(exNotFound!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public async Task ValidateUploadedDocumentAsync_WhenJpegHasValidHeaderButTruncatedBeforeSos_ShouldThrowAndCleanUpStagingBlob()
    {
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"));
        var userId = Guid.NewGuid();
        var upload = await service.GeneratePresignedUploadUrlAsync(userId, "front", "image/jpeg");

        var validMinimalJpeg = KycDocumentStorageService.CreateMinimalImage("image/jpeg");
        // Truncate early after SOF header (e.g. at 200 bytes)
        var truncatedJpeg = validMinimalJpeg[..200];

        service.RegisterSimulatedBlob(upload.BlobName, "image/jpeg", contentLength: truncatedJpeg.Length, content: truncatedJpeg);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, upload.BlobName));

        Assert.That(ex!.Message, Does.Contain("JPEG").And.Contain("missing scan header (SOS)").Or.Contain("truncated"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenStorageThrows404_ShouldConvertIntoInvalidOperationException()
    {
        var fakeBlob = new FakeThrowingBlobClient(new RequestFailedException(404, "Blob not found"));
        var fakeContainer = new FakeThrowingBlobContainerClient(fakeBlob);
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"), blobContainerClient: fakeContainer);
        var userId = Guid.NewGuid();
        var blobName = $"staging/{userId:D}/front/{Guid.NewGuid():N}.jpg";

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, blobName));

        Assert.That(ex!.Message, Does.Contain("does not exist in storage"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenStorageThrows503_ShouldRethrowRequestFailedExceptionDirectlyWithoutWrapping()
    {
        var fakeBlob = new FakeThrowingBlobClient(new RequestFailedException(503, "Service unavailable"));
        var fakeContainer = new FakeThrowingBlobContainerClient(fakeBlob);
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"), blobContainerClient: fakeContainer);
        var userId = Guid.NewGuid();
        var blobName = $"staging/{userId:D}/front/{Guid.NewGuid():N}.jpg";

        var ex = Assert.ThrowsAsync<RequestFailedException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, blobName));

        Assert.That(ex!.Status, Is.EqualTo(503));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenStorageThrowsNetworkOrTimeout_ShouldRethrowDirectlyWithoutWrapping()
    {
        var fakeBlob = new FakeThrowingBlobClient(new TimeoutException("Storage network timeout"));
        var fakeContainer = new FakeThrowingBlobContainerClient(fakeBlob);
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"), blobContainerClient: fakeContainer);
        var userId = Guid.NewGuid();
        var blobName = $"staging/{userId:D}/front/{Guid.NewGuid():N}.jpg";

        var ex = Assert.ThrowsAsync<TimeoutException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, blobName));

        Assert.That(ex!.Message, Does.Contain("Storage network timeout"));
    }

    [Test]
    public void ValidateUploadedDocumentAsync_WhenOperationCanceled_ShouldRethrowOperationCanceledExceptionDirectly()
    {
        var fakeBlob = new FakeThrowingBlobClient(new OperationCanceledException("Operation was canceled"));
        var fakeContainer = new FakeThrowingBlobContainerClient(fakeBlob);
        var service = new KycDocumentStorageService(new ConfigurationBuilder().Build(), new TestHostEnvironment("Development"), blobContainerClient: fakeContainer);
        var userId = Guid.NewGuid();
        var blobName = $"staging/{userId:D}/front/{Guid.NewGuid():N}.jpg";

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.ValidateUploadedDocumentAsync(userId, blobName));
    }
}

internal class FakeThrowingBlobClient : BlobClient
{
    private readonly Exception _exceptionToThrow;
    public FakeThrowingBlobClient(Exception exceptionToThrow) => _exceptionToThrow = exceptionToThrow;

    public override Task<Response<BlobProperties>> GetPropertiesAsync(
        BlobRequestConditions? conditions = null,
        CancellationToken cancellationToken = default)
    {
        throw _exceptionToThrow;
    }
}

internal class FakeThrowingBlobContainerClient : BlobContainerClient
{
    private readonly BlobClient _blobClient;
    public FakeThrowingBlobContainerClient(BlobClient blobClient) => _blobClient = blobClient;

    public override BlobClient GetBlobClient(string blobName) => _blobClient;
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "RoomToRentRadar.Test";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public TestHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;
    public TestTimeProvider(DateTimeOffset initialUtcNow) => _utcNow = initialUtcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
