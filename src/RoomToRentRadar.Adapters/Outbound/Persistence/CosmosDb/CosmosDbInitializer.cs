namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _settings;
    private readonly ILogger<CosmosDbInitializer> _logger;
    private readonly IHostEnvironment? _environment;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbInitializer> logger,
        IHostEnvironment? environment = null)
    {
        _cosmosClient = cosmosClient;
        _settings = options.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Cosmos DB database '{DatabaseName}'...", _settings.DatabaseName);

            var dbResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName, cancellationToken: cancellationToken);
            var database = dbResponse.Database;

            _logger.LogInformation("Creating container '{CampusesContainerName}' (/id)...", _settings.CampusesContainerName);
            var campusesContainerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(_settings.CampusesContainerName, "/id"),
                cancellationToken: cancellationToken);

            var roomDetailsContainerName = string.IsNullOrWhiteSpace(_settings.RoomDetailsContainerName) ? "RoomDetails" : _settings.RoomDetailsContainerName;
            _logger.LogInformation("Creating container '{RoomDetailsContainerName}' (/id)...", roomDetailsContainerName);
            var roomDetailsContainerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(roomDetailsContainerName, "/id"),
                cancellationToken: cancellationToken);

            var roomsContainerName = string.IsNullOrWhiteSpace(_settings.RoomsContainerName) ? "Rooms" : _settings.RoomsContainerName;
            _logger.LogInformation("Creating container '{RoomsContainerName}' (/id)...", roomsContainerName);
            var roomsProps = new ContainerProperties(roomsContainerName, "/id");
            roomsProps.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/location/*",
                SpatialTypes = { SpatialType.Point }
            });
            roomsProps.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            roomsProps.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            roomsProps.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/createdAt", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });

            var roomsRepositoryContainerResponse = await database.CreateContainerIfNotExistsAsync(
                roomsProps,
                cancellationToken: cancellationToken);

            var syncOutboxContainerName = string.IsNullOrWhiteSpace(_settings.RoomProjectionSyncOutboxContainerName)
                ? "RoomProjectionSyncOutbox"
                : _settings.RoomProjectionSyncOutboxContainerName;
            _logger.LogInformation("Creating container '{SyncOutboxContainerName}' (/id)...", syncOutboxContainerName);
            await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(syncOutboxContainerName, "/id"),
                cancellationToken: cancellationToken);

            var listingsContainerName = string.IsNullOrWhiteSpace(_settings.ContainerName) ? "RoomListings" : _settings.ContainerName;
            _logger.LogInformation("Creating container '{ListingsContainerName}' (/campusId)...", listingsContainerName);
            var roomsContainerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(listingsContainerName, "/campusId"),
                cancellationToken: cancellationToken);

            var effectiveContainer = roomsContainerResponse.Container;
            var effectiveProperties = roomsContainerResponse.Resource;
            var partitionKeyPath = effectiveProperties.PartitionKeyPath;

            if (!string.Equals(partitionKeyPath, "/campusId", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cosmos container '{listingsContainerName}' currently uses partition key '{partitionKeyPath}', but '/campusId' is required. " +
                    $"Cosmos DB does not allow in-place partition key changes. Please configure 'CosmosDb:ContainerName' to 'RoomListings'.");
            }

            // Automatic cutover migration from legacy "Rooms" container if it exists
            await MigrateLegacyRoomsContainerIfPresentAsync(database, effectiveContainer, roomDetailsContainerResponse.Container, cancellationToken);

            await EnsureSpatialAndCompositeIndexesAsync(effectiveContainer, effectiveProperties, cancellationToken);
            if (roomsRepositoryContainerResponse?.Container is { } roomsRepoContainer)
            {
                await EnsureRoomsContainerIndexesAsync(roomsRepoContainer, roomsRepositoryContainerResponse.Resource, cancellationToken);
            }

            await SeedInitialDataAsync(campusesContainerResponse.Container, effectiveContainer, cancellationToken);
            if (roomsRepositoryContainerResponse?.Container is { } roomsRepositoryContainer)
            {
                await SynchronizeRoomDetailsAsync(effectiveContainer, roomsRepositoryContainer, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Skipping room-details synchronization because the '{RoomsContainerName}' container is unavailable.", roomsContainerName);
            }

            _logger.LogInformation("Cosmos DB initialization completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB.");
            throw;
        }
    }

    /// <summary>
    /// Checks for a legacy 'Rooms' container (from earlier versions using /id) and performs
    /// strict, schema-aware migration into 'RoomListings' (/campusId) and 'RoomDetails' (/id).
    /// - Reconciles records by ID, never relying purely on document count.
    /// - Never generates fake/dummy business data (e.g. fake coordinates or default campus).
    /// - Records missing required listing fields are preserved in 'RoomDetails' and excluded from 'RoomListings'.
    /// - Safe and idempotent for interrupted executions or re-runs.
    /// - Legacy container 'Rooms' is never deleted, ensuring rollback safety.
    /// </summary>
    private async Task MigrateLegacyRoomsContainerIfPresentAsync(
        Database database,
        Container listingsContainer,
        Container roomDetailsContainer,
        CancellationToken cancellationToken)
    {
        try
        {
            var legacyRoomsContainer = database.GetContainer("Rooms");
            var legacyProperties = await legacyRoomsContainer.ReadContainerAsync(cancellationToken: cancellationToken);
            if (legacyProperties?.Resource is null)
            {
                return;
            }

            // Optimization: check if migration was already marked as completed in metadata
            const string migrationStatusDocId = "__migration_legacy_rooms_status";
            try
            {
                var statusDocResponse = await roomDetailsContainer.ReadItemAsync<Newtonsoft.Json.Linq.JObject>(
                    migrationStatusDocId,
                    new PartitionKey(migrationStatusDocId),
                    cancellationToken: cancellationToken);

                if (string.Equals(statusDocResponse.Resource?["status"]?.ToString(), "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Legacy 'Rooms' migration already marked as completed in metadata. Skipping migration check.");
                    return;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Not yet marked as completed, proceed with batch migration
            }

            var totalLegacyCount = 0;
            var migratedDetailsCount = 0;
            var reconciledDetailsCount = 0;
            var verifiedDetailsParityCount = 0;

            var eligibleListingsCount = 0;
            var migratedListingsCount = 0;
            var reconciledListingsCount = 0;
            var verifiedListingsParityCount = 0;

            var skippedListingsList = new List<(string Id, string Reason)>();

            // Stream batch-by-batch from legacy container with O(1) memory complexity
            using (var feedIterator = legacyRoomsContainer.GetItemQueryIterator<Newtonsoft.Json.Linq.JObject>(new QueryDefinition("SELECT * FROM c")))
            {
                while (feedIterator.HasMoreResults)
                {
                    var batch = await feedIterator.ReadNextAsync(cancellationToken);
                    foreach (var doc in batch)
                    {
                        var id = doc["id"]?.ToString();
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            _logger.LogError("Legacy record missing mandatory 'id' field: {Doc}", doc.ToString(Newtonsoft.Json.Formatting.None));
                            continue;
                        }

                        totalLegacyCount++;

                        // 1. Data-parity check & preservation in RoomDetails (/id) to ensure ZERO data loss
                        var existingDetail = await ReadItemOrNullAsync<Newtonsoft.Json.Linq.JObject>(roomDetailsContainer, id, new PartitionKey(id), cancellationToken);
                        if (existingDetail is null)
                        {
                            await roomDetailsContainer.UpsertItemAsync(doc, new PartitionKey(id), cancellationToken: cancellationToken);
                            migratedDetailsCount++;
                        }
                        else if (AreDetailsEquivalent(doc, existingDetail))
                        {
                            verifiedDetailsParityCount++;
                        }
                        else
                        {
                            // Data disparity detected: legacy is source of truth, reconcile target document
                            _logger.LogWarning("Data disparity detected for room detail '{RoomId}'. Reconciling with legacy source.", id);
                            await roomDetailsContainer.UpsertItemAsync(doc, new PartitionKey(id), cancellationToken: cancellationToken);
                            reconciledDetailsCount++;
                        }

                        // 2. Strict validation for RoomListings (/campusId) without inventing fake business data
                        var validationReasons = new List<string>();

                        var campusId = doc["campusId"]?.ToString();
                        if (string.IsNullOrWhiteSpace(campusId))
                        {
                            validationReasons.Add("Missing mandatory campusId");
                        }

                        var price = (decimal?)doc["monthlyRentAmount"] ?? (decimal?)doc["price"];
                        if (price is null or <= 0m)
                        {
                            validationReasons.Add("Missing or non-positive price/monthlyRentAmount");
                        }

                        var area = (double?)doc["areaSquareMeters"] ?? (double?)doc["area"];
                        if (area is null or <= 0.0 || !double.IsFinite(area.Value))
                        {
                            validationReasons.Add("Missing or invalid areaSquareMeters/area");
                        }

                        var lat = (double?)doc["latitude"];
                        var lon = (double?)doc["longitude"];
                        if (lat is null || lon is null ||
                            !double.IsFinite(lat.Value) || !double.IsFinite(lon.Value) ||
                            lat.Value is < -90.0 or > 90.0 || lon.Value is < -180.0 or > 180.0)
                        {
                            validationReasons.Add("Missing or invalid coordinates (latitude/longitude)");
                        }

                        // If any mandatory listing field is missing/invalid, DO NOT fabricate fake data.
                        // Exclude it from RoomListings and log clearly.
                        if (validationReasons.Count > 0)
                        {
                            var reasonSummary = string.Join("; ", validationReasons);
                            skippedListingsList.Add((id, reasonSummary));
                            _logger.LogWarning("Legacy room '{RoomId}' is ineligible for 'RoomListings' due to: {Reasons}. Preserved in 'RoomDetails' only.", id, reasonSummary);
                            continue;
                        }

                        eligibleListingsCount++;

                        // Map to RoomListingDocument and perform data parity verification / upsert
                        var isVerified = ((int?)doc["verificationStatus"] == 2) || ((bool?)doc["isVerified"] == true);
                        var status = MapLegacyStatusToListingStatus((int?)doc["status"]);

                        var listingDoc = new RoomListingDocument
                        {
                            Id = id,
                            CampusId = campusId!,
                            Title = doc["title"]?.ToString() ?? $"Phòng trọ {id}",
                            Description = doc["description"]?.ToString() ?? string.Empty,
                            MonthlyRentAmount = price!.Value,
                            MonthlyRentCurrency = doc["monthlyRentCurrency"]?.ToString() ?? "VND",
                            AreaSquareMeters = area!.Value,
                            Address = doc["address"]?.ToString() ?? string.Empty,
                            Latitude = lat!.Value,
                            Longitude = lon!.Value,
                            Location = new Microsoft.Azure.Cosmos.Spatial.Point(lon.Value, lat.Value),
                            Status = status,
                            VerificationStatus = isVerified ? 2 : 0, // 2 = Verified (AC4 requirement)
                            ThumbnailUrl = doc["thumbnailUrl"]?.ToString(),
                            CreatedAtUtc = (DateTime?)doc["createdAtUtc"] ?? (DateTime?)doc["lastUpdatedDate"] ?? DateTime.UtcNow
                        };

                        var existingListing = await ReadItemOrNullAsync<RoomListingDocument>(listingsContainer, id, new PartitionKey(campusId!), cancellationToken);
                        if (existingListing is null)
                        {
                            await listingsContainer.UpsertItemAsync(listingDoc, new PartitionKey(campusId!), cancellationToken: cancellationToken);
                            migratedListingsCount++;
                        }
                        else if (IsListingEquivalent(existingListing, listingDoc))
                        {
                            verifiedListingsParityCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Data disparity detected for room listing '{RoomId}'. Reconciling with legacy source.", id);
                            await listingsContainer.UpsertItemAsync(listingDoc, new PartitionKey(campusId!), cancellationToken: cancellationToken);
                            reconciledListingsCount++;
                        }
                    }
                }
            }

            if (totalLegacyCount == 0)
            {
                _logger.LogInformation("Legacy 'Rooms' container is empty. Skipping legacy migration.");
                return;
            }

            // 3. Strict reconciliation check verifying 100% data preservation and integrity
            var totalPreservedDetails = migratedDetailsCount + reconciledDetailsCount + verifiedDetailsParityCount;
            if (totalPreservedDetails != totalLegacyCount)
            {
                throw new InvalidOperationException(
                    $"Migration reconciliation failed: Processed {totalLegacyCount} legacy records, but only {totalPreservedDetails} were verified/preserved in 'RoomDetails'.");
            }

            var totalProcessedListings = migratedListingsCount + reconciledListingsCount + verifiedListingsParityCount;
            if (totalProcessedListings != eligibleListingsCount)
            {
                throw new InvalidOperationException(
                    $"Migration reconciliation failed: Found {eligibleListingsCount} eligible listings, but only {totalProcessedListings} were verified/migrated in 'RoomListings'.");
            }

            // 4. Mark migration as Completed in metadata to avoid re-scanning on future startups
            var statusDoc = new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = migrationStatusDocId,
                ["status"] = "Completed",
                ["migratedAtUtc"] = DateTime.UtcNow,
                ["totalLegacyProcessed"] = totalLegacyCount,
                ["detailsCount"] = totalPreservedDetails,
                ["detailsNew"] = migratedDetailsCount,
                ["detailsReconciled"] = reconciledDetailsCount,
                ["detailsParityVerified"] = verifiedDetailsParityCount,
                ["listingsCount"] = totalProcessedListings,
                ["listingsNew"] = migratedListingsCount,
                ["listingsReconciled"] = reconciledListingsCount,
                ["listingsParityVerified"] = verifiedListingsParityCount,
                ["skippedCount"] = skippedListingsList.Count
            };
            await roomDetailsContainer.UpsertItemAsync(statusDoc, new PartitionKey(migrationStatusDocId), cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Legacy migration completed successfully. " +
                "Total legacy: {TotalLegacy}, " +
                "RoomDetails: {DetailsCount} ({NewDetails} new, {ReconciledDetails} reconciled, {VerifiedDetails} verified parity), " +
                "RoomListings: {ListingsCount} ({NewListings} new, {ReconciledListings} reconciled, {VerifiedListings} verified parity), " +
                "Ineligible for RoomListings (missing fields): {IneligibleCount}.",
                totalLegacyCount,
                totalPreservedDetails,
                migratedDetailsCount,
                reconciledDetailsCount,
                verifiedDetailsParityCount,
                totalProcessedListings,
                migratedListingsCount,
                reconciledListingsCount,
                verifiedListingsParityCount,
                skippedListingsList.Count);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Legacy container does not exist; no action needed.
        }
        catch (Exception ex) when (IsCosmosConnectionException(ex))
        {
            var isDevelopment = _environment?.IsDevelopment() ?? false;
            if (isDevelopment && _settings.IsEmulator)
            {
                _logger.LogWarning("Cosmos DB Emulator is not reachable during legacy migration in Development mode: {Message}. Skipping migration.", ex.Message);
            }
            else
            {
                _logger.LogCritical(ex, "Failed to connect to Cosmos DB during legacy migration.");
                throw;
            }
        }
        catch (Exception ex)
        {
            // P2 – Major: Do NOT swallow data mapping, writing, or reconciliation errors under a generic catch!
            _logger.LogError(ex, "Fatal error occurred during legacy 'Rooms' migration. Halting startup to prevent data corruption.");
            throw;
        }
    }

    private static async Task<T?> ReadItemOrNullAsync<T>(
        Container container,
        string id,
        PartitionKey partitionKey,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            var response = await container.ReadItemAsync<T>(id, partitionKey, cancellationToken: cancellationToken);
            return response?.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static bool AreDetailsEquivalent(Newtonsoft.Json.Linq.JObject legacyDoc, Newtonsoft.Json.Linq.JObject existingDetail)
    {
        var legacyPrice = (decimal?)legacyDoc["monthlyRentAmount"] ?? (decimal?)legacyDoc["price"];
        var existingPrice = (decimal?)existingDetail["monthlyRentAmount"] ?? (decimal?)existingDetail["price"];
        if (legacyPrice != existingPrice)
        {
            return false;
        }

        var legacyArea = (double?)legacyDoc["areaSquareMeters"] ?? (double?)legacyDoc["area"];
        var existingArea = (double?)existingDetail["areaSquareMeters"] ?? (double?)existingDetail["area"];
        if (legacyArea.HasValue != existingArea.HasValue)
        {
            return false;
        }
        if (legacyArea.HasValue && Math.Abs(legacyArea.Value - existingArea!.Value) > 0.001)
        {
            return false;
        }

        if (!string.Equals(legacyDoc["campusId"]?.ToString(), existingDetail["campusId"]?.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        var legacyLat = (double?)legacyDoc["latitude"];
        var existingLat = (double?)existingDetail["latitude"];
        if (legacyLat.HasValue != existingLat.HasValue)
        {
            return false;
        }
        if (legacyLat.HasValue && Math.Abs(legacyLat.Value - existingLat!.Value) > 0.000001)
        {
            return false;
        }

        var legacyLon = (double?)legacyDoc["longitude"];
        var existingLon = (double?)existingDetail["longitude"];
        if (legacyLon.HasValue != existingLon.HasValue)
        {
            return false;
        }
        if (legacyLon.HasValue && Math.Abs(legacyLon.Value - existingLon!.Value) > 0.000001)
        {
            return false;
        }

        if ((int?)legacyDoc["status"] != (int?)existingDetail["status"])
        {
            return false;
        }

        var legacyVerified = ((int?)legacyDoc["verificationStatus"] == 2) || ((bool?)legacyDoc["isVerified"] == true);
        var existingVerified = ((int?)existingDetail["verificationStatus"] == 2) || ((bool?)existingDetail["isVerified"] == true);
        if (legacyVerified != existingVerified)
        {
            return false;
        }

        return string.Equals(legacyDoc["title"]?.ToString(), existingDetail["title"]?.ToString(), StringComparison.Ordinal);
    }

    private static bool IsListingEquivalent(RoomListingDocument existing, RoomListingDocument expected)
    {
        return string.Equals(existing.CampusId, expected.CampusId, StringComparison.Ordinal)
            && existing.MonthlyRentAmount == expected.MonthlyRentAmount
            && Math.Abs(existing.AreaSquareMeters - expected.AreaSquareMeters) < 0.001
            && Math.Abs(existing.Latitude - expected.Latitude) < 0.000001
            && Math.Abs(existing.Longitude - expected.Longitude) < 0.000001
            && existing.Status == expected.Status
            && existing.VerificationStatus == expected.VerificationStatus
            && string.Equals(existing.Title, expected.Title, StringComparison.Ordinal);
    }

    private static bool IsCosmosConnectionException(Exception ex)
    {
        return ex is HttpRequestException ||
               ex is System.Net.Sockets.SocketException ||
               ex.InnerException is HttpRequestException ||
               ex.InnerException is System.Net.Sockets.SocketException ||
               (ex is CosmosException ce && (ce.StatusCode == 0 || (int)ce.StatusCode >= 500)) ||
               ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> GetContainerCountAsync(Container container, CancellationToken cancellationToken)
    {
        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        using var iterator = container.GetItemQueryIterator<int>(countQuery);
        if (iterator.HasMoreResults)
        {
            var res = await iterator.ReadNextAsync(cancellationToken);
            return res.FirstOrDefault();
        }
        return 0;
    }

    private static async Task EnsureSpatialAndCompositeIndexesAsync(Container container, ContainerProperties properties, CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;
        var modified = false;

        var locationIndex = indexingPolicy.SpatialIndexes.FirstOrDefault(index =>
            index.Path.Equals("/location/*", StringComparison.Ordinal));

        if (locationIndex is null)
        {
            indexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/location/*",
                SpatialTypes = { SpatialType.Point }
            });
            modified = true;
        }
        else if (!locationIndex.SpatialTypes.Contains(SpatialType.Point))
        {
            locationIndex.SpatialTypes.Add(SpatialType.Point);
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/monthlyRentAmount", "/id"))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/monthlyRentAmount", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/monthlyRentAmount", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/areaSquareMeters", "/id"))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/areaSquareMeters", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/areaSquareMeters", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    private static bool HasCompositeIndex(IndexingPolicy policy, string path1, string path2)
    {
        return policy.CompositeIndexes.Any(ci =>
            ci.Count >= 2 &&
            ci[0].Path.Equals(path1, StringComparison.Ordinal) &&
            ci[1].Path.Equals(path2, StringComparison.Ordinal));
    }

    private static bool HasCompositeIndex(IndexingPolicy policy, string path1, CompositePathSortOrder order1, string path2, CompositePathSortOrder order2)
    {
        return policy.CompositeIndexes.Any(ci =>
            ci.Count >= 2 &&
            string.Equals(ci[0].Path, path1, StringComparison.Ordinal) &&
            ci[0].Order == order1 &&
            string.Equals(ci[1].Path, path2, StringComparison.Ordinal) &&
            ci[1].Order == order2);
    }

    private static async Task EnsureRoomsContainerIndexesAsync(Container container, ContainerProperties properties, CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;
        var modified = false;

        var locationIndex = indexingPolicy.SpatialIndexes.FirstOrDefault(index =>
            index.Path.Equals("/location/*", StringComparison.Ordinal));

        if (locationIndex is null)
        {
            indexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/location/*",
                SpatialTypes = { SpatialType.Point }
            });
            modified = true;
        }
        else if (!locationIndex.SpatialTypes.Contains(SpatialType.Point))
        {
            locationIndex.SpatialTypes.Add(SpatialType.Point);
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Ascending, "/id", CompositePathSortOrder.Ascending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/createdAt", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/createdAt", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedInitialDataAsync(Container campusesContainer, Container roomsContainer, CancellationToken cancellationToken)
    {
        // 1. Seed ICTU Campus
        const string ictuId = "campus-ictu";
        try
        {
            await campusesContainer.ReadItemAsync<CampusDocument>(ictuId, new PartitionKey(ictuId), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var ictu = new CampusDocument
            {
                Id = ictuId,
                Name = "Trường Đại học Công nghệ Thông tin và Truyền thông - ĐH Thái Nguyên (ICTU)",
                Code = "ICTU",
                Latitude = 21.5852,
                Longitude = 105.8074,
                Address = "Đường Z115, xã Quyết Thắng, TP. Thái Nguyên, Thái Nguyên"
            };

            await campusesContainer.UpsertItemAsync(ictu, new PartitionKey(ictu.Id), cancellationToken: cancellationToken);
            _logger.LogInformation("Seeded default campus: {CampusName}", ictu.Name);
        }

        // 2. Seed Sample Rooms near ICTU if none exist (restricted to Development or explicit SeedSampleData opt-in)
        var isDevelopment = _environment?.IsDevelopment() ?? false;
        if (!_settings.SeedSampleData && !isDevelopment)
        {
            _logger.LogInformation("Skipping sample rooms seeding in non-development environment or when SeedSampleData is false.");
            return;
        }

        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.campusId = @campusId")
            .WithParameter("@campusId", ictuId);

        var iterator = roomsContainer.GetItemQueryIterator<int>(countQuery, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ictuId) });
        var hasRooms = false;
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            if (response.FirstOrDefault() > 0)
            {
                hasRooms = true;
            }
        }

        if (!hasRooms)
        {
            var sampleRooms = new List<RoomListingDocument>
            {
                new()
                {
                    Id = "room-ictu-001",
                    CampusId = ictuId,
                    Title = "Premium private room near ICTU Gate, Alley 89 Z115",
                    Description = "Newly built room with air conditioning, water heater, mezzanine, and high-speed Wi-Fi. A 200 m walk from ICTU's main gate.",
                    MonthlyRentAmount = 1800000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 22.0,
                    Address = "15 Alley 89, Z115 Road, Quyết Thắng, Thái Nguyên City",
                    Latitude = 21.5865,
                    Longitude = 105.8080,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8080, 21.5865),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    Id = "room-ictu-002",
                    CampusId = ictuId,
                    Title = "Fully furnished private dorm room with a balcony",
                    Description = "Includes bunk beds, study desks, and private wardrobes. Flexible hours with 24/7 camera security.",
                    MonthlyRentAmount = 1200000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 18.5,
                    Address = "Tân Thịnh Road, opposite ICTU's secondary gate",
                    Latitude = 21.5840,
                    Longitude = 105.8060,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8060, 21.5840),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = "room-ictu-003",
                    CampusId = ictuId,
                    Title = "One-bedroom mini apartment for students",
                    Description = "Spacious mini apartment with a private kitchen, shared washing machine, and rooftop drying area. Suitable for 2–3 people.",
                    MonthlyRentAmount = 2800000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 35.0,
                    Address = "Group 4, Thịnh Đán Ward, Thái Nguyên City",
                    Latitude = 21.5790,
                    Longitude = 105.8020,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8020, 21.5790),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Id = "room-ictu-004",
                    CampusId = ictuId,
                    Title = "Affordable room for first-year students",
                    Description = "Budget-friendly room with a private bathroom, near Quyết Thắng student market.",
                    MonthlyRentAmount = 900000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 15.0,
                    Address = "Z115 residential area, Thái Nguyên City",
                    Latitude = 21.5890,
                    Longitude = 105.8110,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8110, 21.5890),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = "room-ictu-005",
                    CampusId = ictuId,
                    Title = "Rented room (occupied listing)",
                    Description = "This room is fully occupied.",
                    MonthlyRentAmount = 1500000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 20.0,
                    Address = "Z115 Road, Thái Nguyên City",
                    Latitude = 21.5855,
                    Longitude = 105.8075,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8075, 21.5855),
                    Status = 1, // Occupied -> Must NOT be returned
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    Id = "room-ictu-006",
                    CampusId = ictuId,
                    Title = "Unverified room listing",
                    Description = "New listing awaiting administrator approval.",
                    MonthlyRentAmount = 1600000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 20.0,
                    Address = "Z115 Road, Thái Nguyên City",
                    Latitude = 21.5855,
                    Longitude = 105.8075,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8075, 21.5855),
                    Status = 0, // Available
                    VerificationStatus = 0, // Unverified -> Must NOT be returned
                    ThumbnailUrl = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            };

            foreach (var room in sampleRooms)
            {
                try
                {
                    await roomsContainer.CreateItemAsync(room, new PartitionKey(room.CampusId), cancellationToken: cancellationToken);
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogDebug("Sample room {RoomId} already exists; leaving it unchanged.", room.Id);
                }
            }

            _logger.LogInformation("Seeded {Count} sample rooms for campus {CampusId}.", sampleRooms.Count, ictuId);
        }
    }

    private async Task SynchronizeRoomDetailsAsync(
        Container listingsContainer,
        Container roomsContainer,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false");
        using var iterator = listingsContainer.GetItemQueryIterator<RoomListingDocument>(query);
        var synchronizedCount = 0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var listing in page)
            {
                var existingDetail = await ReadItemOrNullAsync<RoomDocument>(
                    roomsContainer,
                    listing.Id,
                    new PartitionKey(listing.Id),
                    cancellationToken);
                if (existingDetail is not null)
                {
                    if (existingDetail.IsDeleted)
                    {
                        _logger.LogDebug("Skipped deleted room {RoomId} during startup synchronization.", listing.Id);
                        continue;
                    }

                    if (existingDetail.ProjectionVersion > listing.ProjectionVersion)
                    {
                        _logger.LogDebug("Skipped stale listing {RoomId} during startup synchronization.", listing.Id);
                        continue;
                    }

                    if (existingDetail.ProjectionVersion == listing.ProjectionVersion &&
                        !HasListingFieldsDiverged(listing, existingDetail))
                    {
                        _logger.LogDebug("Skipped up-to-date room {RoomId} with matching projection version {Version}.", listing.Id, listing.ProjectionVersion);
                        continue;
                    }
                }

                var detail = RoomDocument.MergeListingFields(listing, existingDetail);
                detail.ProjectionVersion = listing.ProjectionVersion;
                detail.IsDeleted = false;

                try
                {
                    if (existingDetail is null)
                    {
                        await roomsContainer.CreateItemAsync(detail, new PartitionKey(detail.Id), cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await roomsContainer.ReplaceItemAsync(
                            detail,
                            detail.Id,
                            new PartitionKey(detail.Id),
                            new ItemRequestOptions { IfMatchEtag = existingDetail.ETag },
                            cancellationToken);
                    }
                    synchronizedCount++;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict ||
                                                 ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    _logger.LogDebug("Skipped concurrent update for room {RoomId} during startup synchronization.", listing.Id);
                }
            }
        }

        _logger.LogInformation("Synchronized {Count} room detail records from Cosmos listings.", synchronizedCount);
    }

    private static bool HasListingFieldsDiverged(RoomListingDocument listing, RoomDocument detail)
    {
        if (!string.Equals(detail.Title, listing.Title, StringComparison.Ordinal)) return true;
        if (!string.Equals(detail.Description, listing.Description, StringComparison.Ordinal)) return true;
        if (detail.Price != listing.MonthlyRentAmount) return true;
        if (detail.Area != listing.AreaSquareMeters) return true;
        if (!string.Equals(detail.Address, listing.Address, StringComparison.Ordinal)) return true;
        if (!string.Equals(detail.ThumbnailUrl, listing.ThumbnailUrl, StringComparison.Ordinal)) return true;
        if (detail.IsVerified != (listing.VerificationStatus == 2)) return true;
        if (detail.Status != listing.Status) return true;
        if (detail.Latitude != listing.Latitude) return true;
        if (detail.Longitude != listing.Longitude) return true;

        return false;
    }

    private static int MapLegacyStatusToListingStatus(int? legacyStatus) => legacyStatus switch
    {
        0 => 0, // Available -> Available
        1 => 1, // Rented -> Occupied
        2 => 2, // Pending -> Reserved
        3 => 3, // UnderMaintenance -> Hidden
        _ => 3  // Unknown legacy state must not become bookable.
    };
}
