using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.IO.Compression;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using SkiaSharp;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

public sealed class KycDocumentStorageService : IKycDocumentStorageService
{
    public const string DefaultAzuriteAccountName = "devstoreaccount1";
    public const string DefaultAzuriteAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private readonly StorageSharedKeyCredential _credential;
    private readonly string _allowedContainer;
    private readonly string _accountName;
    private readonly HashSet<string> _allowedHosts;
    private readonly TimeProvider _timeProvider;
    private readonly bool _isDevelopmentOrTesting;
    private readonly string _blobBaseUri;
    private readonly BlobContainerClient? _injectedContainerClient;
    private readonly ILogger<KycDocumentStorageService>? _logger;
    private readonly IKycUploadRateLimiter _rateLimiter;
    private readonly ConcurrentDictionary<string, SimulatedBlob> _simulatedBlobs = new(StringComparer.OrdinalIgnoreCase);

    public bool SimulateCopyFailure { get; set; }

    public KycDocumentStorageService(
        IConfiguration? configuration = null,
        IHostEnvironment? environment = null,
        TimeProvider? timeProvider = null,
        BlobContainerClient? blobContainerClient = null,
        ILogger<KycDocumentStorageService>? logger = null,
        IKycUploadRateLimiter? rateLimiter = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _allowedContainer = configuration?["KycStorage:ContainerName"] ?? "kyc";
        _isDevelopmentOrTesting = IsDevelopmentOrTestEnvironment(configuration, environment);

        var (accountName, accountKey) = ResolveCredentials(configuration, _isDevelopmentOrTesting);
        _accountName = accountName;
        _credential = new StorageSharedKeyCredential(accountName, accountKey);
        _allowedHosts = ResolveAllowedHosts(configuration, _accountName, _isDevelopmentOrTesting);
        _blobBaseUri = ResolveBlobBaseUri(configuration, accountName, _isDevelopmentOrTesting);
        _injectedContainerClient = blobContainerClient;
        _logger = logger;
        _rateLimiter = rateLimiter ?? new InMemoryKycUploadRateLimiter(_timeProvider, 10, TimeSpan.FromMinutes(5));
    }

    public void RegisterSimulatedBlob(
        string blobName,
        string contentType = "image/jpeg",
        long contentLength = 0,
        byte[]? content = null)
    {
        var data = content ?? CreateMinimalImage(contentType);
        var length = contentLength > 0 ? contentLength : data.Length;
        _simulatedBlobs[blobName] = new SimulatedBlob(contentType, length, data);
    }

    private static bool IsDevelopmentOrTestEnvironment(IConfiguration? configuration, IHostEnvironment? environment)
    {
        if (environment is not null)
        {
            return environment.IsDevelopment() ||
                   environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase) ||
                   environment.EnvironmentName.Equals("Test", StringComparison.OrdinalIgnoreCase);
        }

        var envName = configuration?["ASPNETCORE_ENVIRONMENT"]
            ?? configuration?["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.IsNullOrWhiteSpace(envName))
        {
            return false;
        }

        return string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(envName, "Testing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(envName, "Test", StringComparison.OrdinalIgnoreCase);
    }

    private static (string AccountName, string AccountKey) ResolveCredentials(
        IConfiguration? configuration,
        bool isDevelopmentOrTest)
    {
        var connString = configuration?["AzureStorage:ConnectionString"]
            ?? configuration?["Storage:ConnectionString"]
            ?? configuration?["ConnectionStrings:AzureStorage"]
            ?? configuration?["KycStorage:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(connString))
        {
            if (connString.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
            {
                if (!isDevelopmentOrTest)
                {
                    throw new InvalidOperationException(
                        "Development storage credentials (UseDevelopmentStorage=true) cannot be used in a Production environment. Explicit Azure Storage credentials are required.");
                }

                return (DefaultAzuriteAccountName, DefaultAzuriteAccountKey);
            }

            var parsed = ParseConnectionString(connString);
            if (parsed.HasValue)
            {
                if (!isDevelopmentOrTest &&
                    string.Equals(parsed.Value.AccountName, DefaultAzuriteAccountName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Default Azurite credentials cannot be used in a Production environment. Explicit Azure Storage credentials are required.");
                }

                return parsed.Value;
            }
        }

        var name = configuration?["KycStorage:AccountName"]
            ?? configuration?["AzureStorage:AccountName"]
            ?? configuration?["Storage:AccountName"];

        var key = configuration?["KycStorage:AccountKey"]
            ?? configuration?["AzureStorage:AccountKey"]
            ?? configuration?["Storage:AccountKey"];

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key))
        {
            if (!isDevelopmentOrTest &&
                string.Equals(name, DefaultAzuriteAccountName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Default Azurite credentials cannot be used in a Production environment. Explicit Azure Storage credentials are required.");
            }

            return (name, key);
        }

        if (!isDevelopmentOrTest)
        {
            throw new InvalidOperationException(
                "KYC document storage credentials (AzureStorage:ConnectionString or KycStorage:AccountName and KycStorage:AccountKey) must be explicitly configured in Production. Fallback to default Azurite credentials is not permitted in Production.");
        }

        return (DefaultAzuriteAccountName, DefaultAzuriteAccountKey);
    }

    private static HashSet<string> ResolveAllowedHosts(
        IConfiguration? configuration,
        string accountName,
        bool isDevelopmentOrTest)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Standard Azure Storage account host
        hosts.Add($"{accountName}.blob.core.windows.net");

        // Local development/Azurite hosts
        if (isDevelopmentOrTest || string.Equals(accountName, DefaultAzuriteAccountName, StringComparison.OrdinalIgnoreCase))
        {
            hosts.Add("127.0.0.1");
            hosts.Add("localhost");
            hosts.Add("azurite");
            hosts.Add($"{DefaultAzuriteAccountName}.blob.core.windows.net");
        }

        // Custom domain or configured endpoints
        var allowedHostsConfig = configuration?["KycStorage:AllowedHosts"]
            ?? configuration?["AzureStorage:AllowedHosts"];
        if (!string.IsNullOrWhiteSpace(allowedHostsConfig))
        {
            foreach (var h in allowedHostsConfig.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                hosts.Add(h);
            }
        }

        var customDomain = configuration?["KycStorage:CustomDomain"]
            ?? configuration?["AzureStorage:CustomDomain"];
        if (!string.IsNullOrWhiteSpace(customDomain))
        {
            hosts.Add(customDomain.Trim());
        }

        var blobEndpoint = configuration?["KycStorage:BlobEndpoint"]
            ?? configuration?["AzureStorage:BlobEndpoint"];
        if (!string.IsNullOrWhiteSpace(blobEndpoint) && !Uri.TryCreate(blobEndpoint, UriKind.Absolute, out _))
        {
            if (!isDevelopmentOrTest)
            {
                throw new InvalidOperationException("KycStorage:BlobEndpoint must be an absolute HTTPS URI in Production.");
            }
        }

        if (!string.IsNullOrWhiteSpace(blobEndpoint) && Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var endpointUri))
        {
            hosts.Add(endpointUri.Host);
        }

        return hosts;
    }

    private static string ResolveBlobBaseUri(
        IConfiguration? configuration,
        string accountName,
        bool isDevelopmentOrTest)
    {
        var blobEndpoint = configuration?["KycStorage:BlobEndpoint"]
            ?? configuration?["AzureStorage:BlobEndpoint"];

        if (!string.IsNullOrWhiteSpace(blobEndpoint) && Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var endpointUri))
        {
            if (!isDevelopmentOrTest && !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("KycStorage:BlobEndpoint must use HTTPS in Production.");
            }

            return endpointUri.ToString().TrimEnd('/');
        }

        if (isDevelopmentOrTest && string.Equals(accountName, DefaultAzuriteAccountName, StringComparison.OrdinalIgnoreCase))
        {
            return $"http://127.0.0.1:10000/{accountName}";
        }

        return $"https://{accountName}.blob.core.windows.net";
    }

    private static (string AccountName, string AccountKey)? ParseConnectionString(string connectionString)
    {
        string? accountName = null;
        string? accountKey = null;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;

            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();

            if (key.Equals("AccountName", StringComparison.OrdinalIgnoreCase))
                accountName = val;
            else if (key.Equals("AccountKey", StringComparison.OrdinalIgnoreCase))
                accountKey = val;
        }

        if (!string.IsNullOrWhiteSpace(accountName) && !string.IsNullOrWhiteSpace(accountKey))
            return (accountName, accountKey);

        return null;
    }

    public void ValidateDocumentUrl(string? rawBlobPath)
    {
        if (string.IsNullOrWhiteSpace(rawBlobPath))
        {
            throw new ArgumentException("The blob path cannot be null or empty.", nameof(rawBlobPath));
        }

        if (!Uri.TryCreate(rawBlobPath.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("The provided blob path is not a valid absolute URI.", nameof(rawBlobPath));
        }

        if (!_isDevelopmentOrTesting && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("KYC document storage requires HTTPS scheme in Production.");
        }

        var blobUriBuilder = new BlobUriBuilder(uri);
        if (string.IsNullOrWhiteSpace(blobUriBuilder.BlobContainerName) || string.IsNullOrWhiteSpace(blobUriBuilder.BlobName))
        {
            throw new ArgumentException("The provided blob URI must contain both a container name and a blob name.", nameof(rawBlobPath));
        }

        // 1. Container validation: EXACT match required (case-insensitive)
        if (!string.Equals(blobUriBuilder.BlobContainerName, _allowedContainer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Blob container '{blobUriBuilder.BlobContainerName}' is not authorized for KYC documents. Expected '{_allowedContainer}'.");
        }

        // 2. Host validation: Host must be in authorized hosts
        if (!_allowedHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException($"Blob host '{uri.Host}' is not authorized for KYC documents.");
        }

        // 3. Storage account validation: If account is specified in the URI, must match configured account
        if (!string.IsNullOrWhiteSpace(blobUriBuilder.AccountName) &&
            !string.Equals(blobUriBuilder.AccountName, _accountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Storage account '{blobUriBuilder.AccountName}' in blob URI does not match configured account '{_accountName}'.");
        }
    }

    public bool IsAuthorizedDocumentUrl(string? rawBlobPath)
    {
        try
        {
            ValidateDocumentUrl(rawBlobPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<KycUploadUrlResponse> GeneratePresignedUploadUrlAsync(
        Guid userId,
        string documentSide,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        }

        var normalizedType = contentType.Trim().ToLowerInvariant();
        var ext = normalizedType switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            _ => throw new InvalidOperationException($"Unsupported content type '{contentType}'. Only image/jpeg and image/png are allowed.")
        };

        var normalizedSide = NormalizeDocumentSide(documentSide);

        // Atomic rate limiting per user (with sliding window and bucket eviction)
        var now = _timeProvider.GetUtcNow();
        await _rateLimiter.CheckAndAcquireAsync(userId, cancellationToken);

        // Clean up previous unpromoted staging uploads for this user and side to avoid orphaned staging storage consumption
        await CleanupStagingBlobsAsync(userId, normalizedSide, cancellationToken);

        // Staging blobs start with staging/{userId:D}/ to allow strict prefix filtering in Storage Lifecycle Policies
        var blobName = $"staging/{userId:D}/{normalizedSide}/{Guid.NewGuid():N}{ext}";
        var expiresOn = now.AddMinutes(10);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _allowedContainer,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now.AddMinutes(-5),
            ExpiresOn = expiresOn,
            Protocol = ResolveSasProtocol()
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Create);

        var fullUri = new Uri($"{_blobBaseUri}/{_allowedContainer}/{blobName}");
        var blobUriBuilder = new BlobUriBuilder(fullUri)
        {
            Sas = sasBuilder.ToSasQueryParameters(_credential)
        };

        var uploadUrl = blobUriBuilder.ToUri().ToString();

        return new KycUploadUrlResponse(blobName, uploadUrl, expiresOn);
    }

    public async Task ValidateUploadedDocumentAsync(
        Guid userId,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("The blob name cannot be null or empty.", nameof(blobName));
        }

        var expectedPrefix = $"staging/{userId:D}/";
        var legacyPrefix = $"{userId:D}/kyc/staging/";
        if (!blobName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
            !blobName.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The KYC document does not belong to the current user.");
        }

        var ext = Path.GetExtension(blobName);
        if (!string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid KYC document file extension '{ext}'. Only JPEG and PNG images are permitted.");
        }

        const long maxSizeBytes = 5 * 1024 * 1024; // 5 MB
        string contentType;
        long contentLength;
        byte[] imageContent;

        if (_injectedContainerClient is not null)
        {
            var blobClient = _injectedContainerClient.GetBlobClient(blobName);
            try
            {
                var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                contentType = props.Value.ContentType;
                contentLength = props.Value.ContentLength;

                if (contentLength > maxSizeBytes)
                {
                    await TryDeleteBlobAsync(blobName, cancellationToken);
                    throw new InvalidOperationException($"KYC document file size ({contentLength} bytes) exceeds the maximum allowed limit of 5 MB.");
                }

                imageContent = await ReadBlobContentAsync(blobClient, contentLength, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new InvalidOperationException("The KYC document blob does not exist in storage.", ex);
            }
            catch (RequestFailedException)
            {
                // Azure Storage infrastructure errors must bubble up directly so they are handled as 5xx
                throw;
            }
            catch (OperationCanceledException)
            {
                // Cancellation must bubble up directly
                throw;
            }
        }
        else if (_isDevelopmentOrTesting)
        {
            if (_simulatedBlobs.TryGetValue(blobName, out var sim))
            {
                contentType = sim.ContentType;
                contentLength = sim.ContentLength;

                if (contentLength > maxSizeBytes)
                {
                    await TryDeleteBlobAsync(blobName, cancellationToken);
                    throw new InvalidOperationException($"KYC document file size ({contentLength} bytes) exceeds the maximum allowed limit of 5 MB.");
                }

                imageContent = sim.Content;
            }
            else
            {
                throw new InvalidOperationException("The KYC document blob does not exist in storage.");
            }
        }
        else
        {
            try
            {
                var serviceClient = new BlobServiceClient(new Uri(_blobBaseUri), _credential);
                var containerClient = serviceClient.GetBlobContainerClient(_allowedContainer);
                var blobClient = containerClient.GetBlobClient(blobName);

                var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                contentType = props.Value.ContentType;
                contentLength = props.Value.ContentLength;

                if (contentLength > maxSizeBytes)
                {
                    await TryDeleteBlobAsync(blobName, cancellationToken);
                    throw new InvalidOperationException($"KYC document file size ({contentLength} bytes) exceeds the maximum allowed limit of 5 MB.");
                }

                imageContent = await ReadBlobContentAsync(blobClient, contentLength, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new InvalidOperationException("The KYC document blob does not exist in storage.", ex);
            }
            catch (RequestFailedException)
            {
                // Azure Storage infrastructure errors must bubble up directly so they are handled as 5xx
                throw;
            }
            catch (OperationCanceledException)
            {
                // Cancellation must bubble up directly
                throw;
            }
        }

        if (!string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            await TryDeleteBlobAsync(blobName, cancellationToken);
            throw new InvalidOperationException($"Invalid KYC document content type '{contentType}'. Only image/jpeg and image/png are permitted.");
        }

        try
        {
            ValidateImageContent(imageContent, contentType);
        }
        catch
        {
            await TryDeleteBlobAsync(blobName, cancellationToken);
            throw;
        }
    }

    public async Task<string> ValidateAndPromoteUploadedDocumentAsync(
        Guid userId,
        string documentSide,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var normalizedSide = NormalizeDocumentSide(documentSide);
        var expectedPrefix = $"staging/{userId:D}/{normalizedSide}/";
        var legacyPrefix = $"{userId:D}/kyc/staging/{normalizedSide}/";
        if (!blobName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
            !blobName.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The KYC document is not a staging upload for the {normalizedSide} side.");
        }

        await ValidateUploadedDocumentAsync(userId, blobName, cancellationToken);
        var verifiedBlobName = blobName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
            ? blobName.Replace(expectedPrefix, $"verified/{userId:D}/{normalizedSide}/", StringComparison.OrdinalIgnoreCase)
            : blobName.Replace(legacyPrefix, $"{userId:D}/kyc/verified/{normalizedSide}/", StringComparison.OrdinalIgnoreCase);

        if (_injectedContainerClient is not null)
        {
            var source = _injectedContainerClient.GetBlobClient(blobName);
            var destination = _injectedContainerClient.GetBlobClient(verifiedBlobName);
            var copy = await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken);
            await copy.WaitForCompletionAsync(cancellationToken);

            var destProps = await destination.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!copy.HasValue || destProps.Value.CopyStatus != CopyStatus.Success)
            {
                throw new RequestFailedException(
                    $"Failed to promote KYC document to verified storage. Copy status: '{destProps.Value.CopyStatus}'. Staging document was preserved.");
            }

            await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        else if (_isDevelopmentOrTesting)
        {
            if (SimulateCopyFailure)
            {
                throw new RequestFailedException("Failed to promote KYC document to verified storage. Copy status: 'Failed'. Staging document was preserved.");
            }

            if (!_simulatedBlobs.TryGetValue(blobName, out var simulatedBlob))
            {
                throw new InvalidOperationException("The KYC document blob does not exist in storage.");
            }

            _simulatedBlobs[verifiedBlobName] = simulatedBlob;
            _simulatedBlobs.TryRemove(blobName, out _);
        }
        else
        {
            var serviceClient = new BlobServiceClient(new Uri(_blobBaseUri), _credential);
            var containerClient = serviceClient.GetBlobContainerClient(_allowedContainer);
            var source = containerClient.GetBlobClient(blobName);
            var destination = containerClient.GetBlobClient(verifiedBlobName);
            var copy = await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken);
            await copy.WaitForCompletionAsync(cancellationToken);

            var destProps = await destination.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (!copy.HasValue || destProps.Value.CopyStatus != CopyStatus.Success)
            {
                throw new RequestFailedException(
                    $"Failed to promote KYC document to verified storage. Copy status: '{destProps.Value.CopyStatus}'. Staging document was preserved.");
            }

            await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        return verifiedBlobName;
    }

    public Task<string?> GeneratePresignedReadUrlAsync(
        string? rawBlobPath,
        TimeSpan validDuration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawBlobPath))
        {
            return Task.FromResult<string?>(null);
        }

        BlobUriBuilder blobUriBuilder;
        if (Uri.TryCreate(rawBlobPath.Trim(), UriKind.Absolute, out var uri))
        {
            ValidateDocumentUrl(rawBlobPath);
            blobUriBuilder = new BlobUriBuilder(uri);
        }
        else
        {
            var cleanBlobName = rawBlobPath.Trim().TrimStart('/');
            var fullUri = new Uri($"{_blobBaseUri}/{_allowedContainer}/{cleanBlobName}");
            blobUriBuilder = new BlobUriBuilder(fullUri);
        }

        var duration = validDuration > TimeSpan.Zero ? validDuration : TimeSpan.FromMinutes(15);
        var now = _timeProvider.GetUtcNow();
        var expiresOn = now.Add(duration);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobUriBuilder.BlobContainerName,
            BlobName = blobUriBuilder.BlobName,
            Resource = "b",
            StartsOn = now.AddMinutes(-5),
            ExpiresOn = expiresOn,
            Protocol = ResolveSasProtocol()
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasQueryParameters = sasBuilder.ToSasQueryParameters(_credential);
        blobUriBuilder.Sas = sasQueryParameters;

        return Task.FromResult<string?>(blobUriBuilder.ToUri().ToString());
    }

    private SasProtocol ResolveSasProtocol()
    {
        if (!_isDevelopmentOrTesting)
        {
            return SasProtocol.Https; // Production always requires HTTPS
        }

        return _blobBaseUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? SasProtocol.HttpsAndHttp
            : SasProtocol.Https;
    }

    private static string NormalizeDocumentSide(string documentSide)
    {
        var normalizedSide = documentSide?.Trim().ToLowerInvariant();
        if (normalizedSide is not "front" and not "back")
        {
            throw new InvalidOperationException("Document side must be front or back.");
        }

        return normalizedSide;
    }

    public async Task DeleteBlobIfExistsAsync(string? blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        try
        {
            if (_injectedContainerClient is not null)
            {
                var blobClient = _injectedContainerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            else if (_isDevelopmentOrTesting)
            {
                _simulatedBlobs.TryRemove(blobName, out _);
            }
            else
            {
                var serviceClient = new BlobServiceClient(new Uri(_blobBaseUri), _credential);
                var containerClient = serviceClient.GetBlobContainerClient(_allowedContainer);
                var blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete blob '{BlobName}' from KYC storage.", blobName);
        }
    }

    private async Task TryDeleteBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        await DeleteBlobIfExistsAsync(blobName, cancellationToken);
    }

    private async Task CleanupStagingBlobsAsync(Guid userId, string normalizedSide, CancellationToken cancellationToken)
    {
        var prefixes = new[]
        {
            $"staging/{userId:D}/{normalizedSide}/",
            $"{userId:D}/kyc/staging/{normalizedSide}/"
        };

        foreach (var prefix in prefixes)
        {
            try
            {
                if (_injectedContainerClient is not null)
                {
                    await foreach (var blobItem in _injectedContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
                    {
                        await _injectedContainerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken);
                    }
                }
                else if (_isDevelopmentOrTesting)
                {
                    var keysToRemove = _simulatedBlobs.Keys
                        .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    foreach (var key in keysToRemove)
                    {
                        _simulatedBlobs.TryRemove(key, out _);
                    }
                }
                else
                {
                    var serviceClient = new BlobServiceClient(new Uri(_blobBaseUri), _credential);
                    var containerClient = serviceClient.GetBlobContainerClient(_allowedContainer);
                    await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
                    {
                        await containerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken);
                    }
                }
            }
            catch
            {
                // Non-blocking cleanup
            }
        }
    }

    private static async Task<byte[]> ReadBlobContentAsync(BlobClient blobClient, long contentLength, CancellationToken cancellationToken)
    {
        var capacity = (int)Math.Min(contentLength, 5 * 1024 * 1024);
        using var ms = new MemoryStream(capacity);
        await blobClient.DownloadToAsync(ms, cancellationToken: cancellationToken);
        return ms.ToArray();
    }

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = InitializeCrcTable();

    private static uint[] InitializeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                if ((c & 1) != 0)
                {
                    c = 0xEDB88320U ^ (c >> 1);
                }
                else
                {
                    c >>= 1;
                }
            }
            table[n] = c;
        }
        return table;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFU;
        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFU;
    }

    private static void ValidateImageContent(ReadOnlySpan<byte> content, string contentType)
    {
        const int maxDimension = 10_000;
        const long maxPixels = 25_000_000; // 25 Megapixels limit (e.g. 5000 x 5000)

        if (content.Length < 16)
        {
            throw new InvalidOperationException("KYC document content is not a valid JPEG or PNG image: file is too small to be a valid image.");
        }

        var isPng = string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase);
        var isJpeg = string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(contentType, "image/jpg", StringComparison.OrdinalIgnoreCase);

        if (!isPng && !isJpeg)
        {
            throw new InvalidOperationException($"Invalid KYC document content type '{contentType}'. Only image/jpeg and image/png are permitted.");
        }

        if (isPng)
        {
            ValidatePngStructureAndChunks(content);
        }
        else
        {
            ValidateJpegStructure(content);
        }

        using var ms = new MemoryStream(content.ToArray(), writable: false);
        using var codec = SKCodec.Create(ms);
        if (codec is null)
        {
            throw new InvalidOperationException("KYC document content is not a valid JPEG or PNG image.");
        }

        var format = codec.EncodedFormat;
        if (format == SKEncodedImageFormat.Png)
        {
            if (!isPng)
            {
                throw new InvalidOperationException("KYC document content does not match its declared image type.");
            }

            ValidatePngDecompressionBounds(content, codec.Info.Width, codec.Info.Height, maxDimension, maxPixels);
        }
        else if (format == SKEncodedImageFormat.Jpeg)
        {
            if (!isJpeg)
            {
                throw new InvalidOperationException("KYC document content does not match its declared image type.");
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported image format '{format}'. Only JPEG and PNG are allowed.");
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;

        if (width <= 0 || height <= 0 || width > maxDimension || height > maxDimension)
        {
            throw new InvalidOperationException("KYC document image dimensions are invalid or exceed the allowed limit of 10,000 pixels.");
        }

        var totalPixels = (long)width * height;
        if (totalPixels > maxPixels)
        {
            throw new InvalidOperationException($"KYC document image resolution ({width}x{height} = {totalPixels} pixels) exceeds the maximum allowed limit of {maxPixels} pixels.");
        }

        // Fully decode bitmap using Skia (validates all scanlines, entropy scan data, and bitstream completeness)
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null || bitmap.Width != width || bitmap.Height != height)
        {
            throw new InvalidOperationException("KYC document image could not be fully decoded or contains corrupt/truncated image scan data.");
        }
    }

    private static void ValidatePngStructureAndChunks(ReadOnlySpan<byte> content)
    {
        if (content.Length < 8 || !content[..8].SequenceEqual(PngSignature))
        {
            throw new InvalidOperationException("KYC document PNG image header is invalid.");
        }

        var offset = 8;
        var hasIhdr = false;
        var hasIdat = false;
        var hasIend = false;

        while (offset + 8 <= content.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset, 4));
            offset += 4;
            var typeSpan = content.Slice(offset, 4);
            var typeCode = BinaryPrimitives.ReadUInt32BigEndian(typeSpan);
            var typeName = System.Text.Encoding.ASCII.GetString(typeSpan);
            offset += 4;

            if (length < 0 || offset + length + 4 > content.Length)
            {
                throw new InvalidOperationException($"KYC document PNG chunk '{typeName}' is truncated.");
            }

            var chunkData = content.Slice(offset, length);
            var actualCrc = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset + length, 4));
            var expectedCrc = ComputeCrc32(typeSpan, chunkData);

            if (actualCrc != expectedCrc)
            {
                throw new InvalidOperationException($"KYC document PNG chunk '{typeName}' CRC checksum verification failed.");
            }

            if (typeCode == 0x49484452) // IHDR
            {
                hasIhdr = true;
            }
            else if (typeCode == 0x49444154) // IDAT
            {
                hasIdat = true;
            }
            else if (typeCode == 0x49454E44) // IEND
            {
                hasIend = true;
                break;
            }

            offset += length + 4;
        }

        if (!hasIhdr)
        {
            throw new InvalidOperationException("KYC document PNG image is missing header chunk (IHDR).");
        }

        if (!hasIdat)
        {
            throw new InvalidOperationException("KYC document PNG image is missing image data (IDAT).");
        }

        if (!hasIend)
        {
            throw new InvalidOperationException("KYC document PNG image is missing end chunk (IEND).");
        }
    }

    private static void ValidateJpegStructure(ReadOnlySpan<byte> content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8)
        {
            throw new InvalidOperationException("KYC document JPEG image header is invalid.");
        }

        var offset = 2;
        var hasSof = false;
        var hasSos = false;
        var hasEoi = false;

        while (offset + 1 < content.Length)
        {
            if (content[offset] != 0xFF)
            {
                break;
            }

            while (offset < content.Length && content[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= content.Length)
            {
                break;
            }

            var marker = content[offset++];
            if (marker == 0xD9) // EOI
            {
                hasEoi = true;
                break;
            }

            if (marker is >= 0xD0 and <= 0xD7) // RST markers
            {
                continue;
            }

            if (marker == 0xDA) // SOS
            {
                hasSos = true;
                if (offset + 2 > content.Length)
                {
                    break;
                }

                var sosLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
                offset += sosLength;

                // Scan entropy data until next marker
                var eoiFound = false;
                while (offset < content.Length)
                {
                    if (content[offset++] == 0xFF)
                    {
                        while (offset < content.Length && content[offset] == 0xFF)
                        {
                            offset++;
                        }

                        if (offset >= content.Length)
                        {
                            throw new InvalidOperationException("KYC document JPEG image is truncated: missing End Of Image (EOI) marker.");
                        }

                        var scanMarker = content[offset++];
                        if (scanMarker == 0x00 || scanMarker is >= 0xD0 and <= 0xD7)
                        {
                            continue;
                        }

                        if (scanMarker == 0xD9)
                        {
                            eoiFound = true;
                            hasEoi = true;
                            break;
                        }

                        offset -= 2;
                        break;
                    }
                }

                if (eoiFound)
                {
                    break;
                }
            }
            else
            {
                if (offset + 2 > content.Length)
                {
                    break;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
                if (length < 2 || offset + length > content.Length)
                {
                    break;
                }

                if (marker is 0xC0 or 0xC1 or 0xC2)
                {
                    hasSof = true;
                }

                offset += length;
            }
        }

        if (!hasSof)
        {
            throw new InvalidOperationException("KYC document JPEG image is missing frame header (SOF).");
        }

        if (!hasSos)
        {
            throw new InvalidOperationException("KYC document JPEG image is missing scan header (SOS).");
        }

        if (!hasEoi)
        {
            throw new InvalidOperationException("KYC document JPEG image is truncated: missing End Of Image (EOI) marker.");
        }
    }

    private static void ValidatePngDecompressionBounds(
        ReadOnlySpan<byte> content,
        int width,
        int height,
        int maxDimension,
        long maxPixels)
    {
        if (width <= 0 || height <= 0 || width > maxDimension || height > maxDimension)
        {
            throw new InvalidOperationException("KYC document image dimensions are invalid or exceed the allowed limit.");
        }

        var totalPixels = (long)width * height;
        if (totalPixels > maxPixels)
        {
            throw new InvalidOperationException("KYC document image resolution exceeds allowed limit.");
        }

        if (content.Length < 33 || !content[..8].SequenceEqual(PngSignature))
        {
            throw new InvalidOperationException("KYC document PNG image header is invalid.");
        }

        var bitDepth = content[24];
        var colorType = content[25];

        var channels = colorType switch
        {
            0 => 1, // Grayscale
            2 => 3, // Truecolor / RGB
            3 => 1, // Indexed-color
            4 => 2, // Grayscale with alpha
            6 => 4, // Truecolor with alpha / RGBA
            _ => throw new InvalidOperationException($"Unsupported PNG color type '{colorType}'.")
        };

        if (bitDepth is not (1 or 2 or 4 or 8 or 16))
        {
            throw new InvalidOperationException($"Unsupported PNG bit depth '{bitDepth}'.");
        }

        var bpp = channels * bitDepth;
        var bytesPerScanline = (width * bpp + 7) / 8;
        var expectedDecompressedBytes = (long)height * (1 + bytesPerScanline);

        const long maxDecompressedBytesLimit = 100 * 1024 * 1024; // 100 MB max uncompressed limit
        if (expectedDecompressedBytes <= 0 || expectedDecompressedBytes > maxDecompressedBytesLimit)
        {
            throw new InvalidOperationException(
                $"KYC document PNG image decompression limit exceeded: calculated payload of {expectedDecompressedBytes} bytes exceeds the maximum allowed limit of {maxDecompressedBytesLimit} bytes.");
        }

        // Scan all IDAT chunks and decompress with a bounded, streaming reader
        using var idatStream = new MemoryStream();
        var offset = 8;
        while (offset + 8 <= content.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset, 4));
            offset += 4;
            var typeSpan = content.Slice(offset, 4);
            var typeCode = BinaryPrimitives.ReadUInt32BigEndian(typeSpan);
            offset += 4;

            if (offset + length + 4 > content.Length)
            {
                break;
            }

            if (typeCode == 0x49444154) // IDAT
            {
                idatStream.Write(content.Slice(offset, (int)length));
            }

            offset += (int)length + 4; // skip data + 4 bytes CRC
        }

        if (idatStream.Length == 0)
        {
            throw new InvalidOperationException("KYC document PNG image is missing image data (IDAT).");
        }

        idatStream.Position = 0;
        try
        {
            using var zlib = new ZLibStream(idatStream, CompressionMode.Decompress);
            var buffer = new byte[8192];
            long totalDecompressed = 0;
            while (true)
            {
                var read = zlib.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                totalDecompressed += read;
                if (totalDecompressed > expectedDecompressedBytes)
                {
                    throw new InvalidOperationException(
                        $"KYC document PNG decompression bomb detected: decompressed data ({totalDecompressed} bytes) exceeds expected image payload of {expectedDecompressedBytes} bytes.");
                }
            }

            if (totalDecompressed != expectedDecompressedBytes)
            {
                throw new InvalidOperationException(
                    $"KYC document PNG image is corrupt or truncated: decompressed size ({totalDecompressed} bytes) does not match expected size ({expectedDecompressedBytes} bytes).");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"KYC document PNG image data is corrupt or cannot be decompressed: {ex.Message}", ex);
        }
    }

    private static readonly byte[] MinimalValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static readonly byte[] MinimalValidJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD5/ooooA//2Q==");

    public static byte[] CreateMinimalImage(string contentType) =>
        string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase)
            ? (byte[])MinimalValidPng.Clone()
            : (byte[])MinimalValidJpeg.Clone();

    private sealed record SimulatedBlob(string ContentType, long ContentLength, byte[] Content);
}
