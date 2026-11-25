using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;

public sealed class CosmosDbContext : ICosmosDbContext
{
    private readonly CosmosClient? _cosmosClient;
    private readonly CosmosDbOptions _options;
    private readonly bool _isInMemory;
    private readonly bool _ownsCosmosClient;
    private readonly SemaphoreSlim _favouritesInitLock = new(1, 1);
    private volatile bool _favouritesContainerEnsured;

    private readonly ICosmosContainer<UserDocument> _usersContainer;
    private readonly ICosmosContainer<RefreshTokenDocument> _refreshTokensContainer;
    private readonly ICosmosContainer<EmailIndexDocument> _emailIndexContainer;
    private readonly ICosmosContainer<ExternalLoginIndexDocument> _externalLoginIndexContainer;
    private readonly ICosmosContainer<UserProfileDocument> _userProfilesContainer;
    private readonly ICosmosContainer<FavouriteDocument> _favouritesContainer;
    private readonly ICosmosContainer<RoomHouseRulesDocument> _houseRulesContainer;
    private readonly ICosmosContainer<ListingReportDocument> _listingReportsContainer;
    private readonly Container? _roomsCosmosContainer;

    public const string PartitionKeyPath = "/id";
    public const string FavouritesPartitionKeyPath = "/userId";
    public const string HouseRulesPartitionKeyPath = "/roomId";
    public const string ListingReportsPartitionKeyPath = "/roomId";
    public const string UniqueTokenHashPath = "/tokenHash";

    public CosmosDbContext(IOptions<CosmosDbOptions> options)
        : this(options.Value, null)
    {
    }

    public CosmosDbContext(CosmosDbOptions options)
        : this(options, null)
    {
    }

    public CosmosDbContext(CosmosDbOptions options, CosmosClient? sharedCosmosClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        // In-memory mode is ONLY allowed when AccountEndpoint is explicitly set to "in-memory".
        // Any other missing configuration is a misconfiguration and must fail fast.
        _isInMemory = string.Equals(options.AccountEndpoint, "in-memory", StringComparison.OrdinalIgnoreCase);

        if (_isInMemory)
        {
            _usersContainer = new InMemoryCosmosContainer<UserDocument>();
            _refreshTokensContainer = new InMemoryCosmosContainer<RefreshTokenDocument>();
            _emailIndexContainer = new InMemoryCosmosContainer<EmailIndexDocument>();
            _externalLoginIndexContainer = new InMemoryCosmosContainer<ExternalLoginIndexDocument>();
            _userProfilesContainer = new InMemoryCosmosContainer<UserProfileDocument>();
            _favouritesContainer = new InMemoryCosmosContainer<FavouriteDocument>();
            _houseRulesContainer = new InMemoryCosmosContainer<RoomHouseRulesDocument>();
            _listingReportsContainer = new InMemoryCosmosContainer<ListingReportDocument>();
            _roomsCosmosContainer = null;
            _favouritesContainerEnsured = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.AccountEndpoint))
            {
                throw new InvalidOperationException(
                    "Azure Cosmos DB is not configured. 'CosmosDb:AccountEndpoint' is required. " +
                    "Set the endpoint in configuration or use CosmosDbContext.CreateInMemory() for local testing.");
            }

            if (string.IsNullOrWhiteSpace(options.AuthKeyOrResourceToken) && string.IsNullOrWhiteSpace(options.AuthKey))
            {
                throw new InvalidOperationException(
                    "Azure Cosmos DB is not configured. 'CosmosDb:AuthKeyOrResourceToken' is required. " +
                    "Set the key in configuration or use CosmosDbContext.CreateInMemory() for local testing.");
            }
            var clientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };

            if (string.Equals(options.ConnectionMode, "Gateway", StringComparison.OrdinalIgnoreCase) ||
                options.AccountEndpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                options.AccountEndpoint.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
            }

            if (options.IsEmulator)
            {
                var isLocalEndpoint = Uri.TryCreate(options.AccountEndpoint, UriKind.Absolute, out var endpointUri) &&
                                      (endpointUri.IsLoopback ||
                                       string.Equals(endpointUri.Host, "localhost", StringComparison.OrdinalIgnoreCase));

                if (!isLocalEndpoint)
                {
                    throw new InvalidOperationException(
                        $"Cosmos DB 'IsEmulator' configuration is strictly allowed only for local loopback endpoints (e.g., localhost). " +
                        $"Configured endpoint '{options.AccountEndpoint}' is not a local endpoint, and TLS certificate bypass has been blocked.");
                }

                clientOptions.ConnectionMode = ConnectionMode.Gateway;
                clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });
            }
            else if (options.DisableTlsValidation)
            {
                clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });
            }

            var authKey = !string.IsNullOrWhiteSpace(options.AuthKey)
                ? options.AuthKey
                : options.AuthKeyOrResourceToken;

            _cosmosClient = sharedCosmosClient ?? new CosmosClient(options.AccountEndpoint, authKey, clientOptions);
            _ownsCosmosClient = sharedCosmosClient is null;
            var favouritesContainerName = string.IsNullOrWhiteSpace(options.FavouritesContainerName)
                ? "Favourites"
                : options.FavouritesContainerName;

            var usersCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.UsersContainerName);
            var refreshTokensCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.RefreshTokensContainerName);
            var emailIndexCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.EmailIndexContainerName);
            var externalLoginIndexCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.ExternalLoginIndexContainerName);
            var userProfilesCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.UserProfilesContainerName);
            var favouritesCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, favouritesContainerName);
            var houseRulesContainerName = string.IsNullOrWhiteSpace(options.HouseRulesContainerName)
                ? "RoomHouseRules"
                : options.HouseRulesContainerName;
            var houseRulesCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, houseRulesContainerName);
            var listingReportsContainerName = string.IsNullOrWhiteSpace(options.ListingReportsContainerName)
                ? "ListingReports"
                : options.ListingReportsContainerName;
            var listingReportsCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, listingReportsContainerName);
            _roomsCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.RoomsContainerName);

            _usersContainer = new CosmosContainer<UserDocument>(usersCosmosContainer);
            _refreshTokensContainer = new CosmosContainer<RefreshTokenDocument>(refreshTokensCosmosContainer);
            _emailIndexContainer = new CosmosContainer<EmailIndexDocument>(emailIndexCosmosContainer);
            _externalLoginIndexContainer = new CosmosContainer<ExternalLoginIndexDocument>(externalLoginIndexCosmosContainer);
            _userProfilesContainer = new CosmosContainer<UserProfileDocument>(userProfilesCosmosContainer);
            _favouritesContainer = new CosmosContainer<FavouriteDocument>(favouritesCosmosContainer);
            _houseRulesContainer = new CosmosContainer<RoomHouseRulesDocument>(houseRulesCosmosContainer);
            _listingReportsContainer = new CosmosContainer<ListingReportDocument>(listingReportsCosmosContainer);
        }
    }

    public ICosmosContainer<UserDocument> Users => _usersContainer;

    public ICosmosContainer<RefreshTokenDocument> RefreshTokens => _refreshTokensContainer;

    public ICosmosContainer<EmailIndexDocument> EmailIndex => _emailIndexContainer;

    public ICosmosContainer<ExternalLoginIndexDocument> ExternalLoginIndex => _externalLoginIndexContainer;

    public ICosmosContainer<UserProfileDocument> UserProfiles => _userProfilesContainer;

    public ICosmosContainer<FavouriteDocument> Favourites => _favouritesContainer;

    public ICosmosContainer<RoomHouseRulesDocument> HouseRules => _houseRulesContainer;

    public ICosmosContainer<ListingReportDocument> ListingReports => _listingReportsContainer;

    public Container? RoomsContainer => _roomsCosmosContainer;

    public CosmosClient? Client => _cosmosClient;

    public static CosmosDbContext CreateInMemory()
    {
        return new CosmosDbContext(new CosmosDbOptions
        {
            AccountEndpoint = "in-memory",
            DatabaseName = "RoomToRentRadarDb"
        });
    }

    public async Task EnsureFavouritesContainerAsync(CancellationToken cancellationToken = default)
    {
        if (_isInMemory || _cosmosClient is null || _favouritesContainerEnsured)
        {
            return;
        }

        await _favouritesInitLock.WaitAsync(cancellationToken);
        try
        {
            if (_favouritesContainerEnsured)
            {
                return;
            }

            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                cancellationToken: cancellationToken);

            await EnsureFavouritesContainerOnDatabaseAsync(databaseResponse.Database, cancellationToken);
            _favouritesContainerEnsured = true;
        }
        finally
        {
            _favouritesInitLock.Release();
        }
    }

    public async Task EnsureDatabaseAndContainersAsync(CancellationToken cancellationToken = default)
    {
        if (_isInMemory || _cosmosClient is null)
        {
            return;
        }

        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
            _options.DatabaseName,
            cancellationToken: cancellationToken);

        var database = databaseResponse.Database;

        var listingsContainerName = string.IsNullOrWhiteSpace(_options.ContainerName) ? "RoomListings" : _options.ContainerName;
        var listingsContainerProperties = new ContainerProperties(listingsContainerName, "/campusId");
        listingsContainerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
        {
            Path = "/location/*",
            SpatialTypes = { SpatialType.Point }
        });
        listingsContainerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
        {
            Path = "/Location/*",
            SpatialTypes = { SpatialType.Point }
        });

        var listingsResponse = await database.CreateContainerIfNotExistsAsync(
            listingsContainerProperties,
            cancellationToken: cancellationToken);

        await EnsureSpatialIndexesAsync(listingsResponse.Container, listingsResponse.Resource, cancellationToken);

        var syncOutboxContainerName = string.IsNullOrWhiteSpace(_options.RoomProjectionSyncOutboxContainerName)
            ? "RoomProjectionSyncOutbox"
            : _options.RoomProjectionSyncOutboxContainerName;
        await database.CreateContainerIfNotExistsAsync(syncOutboxContainerName, PartitionKeyPath, cancellationToken: cancellationToken);

        var roomDetailsContainerName = string.IsNullOrWhiteSpace(_options.RoomDetailsContainerName) ? "RoomDetails" : _options.RoomDetailsContainerName;
        await database.CreateContainerIfNotExistsAsync(roomDetailsContainerName, PartitionKeyPath, cancellationToken: cancellationToken);
        var usersContainerProperties = new ContainerProperties(_options.UsersContainerName, PartitionKeyPath);

        await database.CreateContainerIfNotExistsAsync(
            usersContainerProperties,
            cancellationToken: cancellationToken);

        var refreshTokensContainerProperties = new ContainerProperties(_options.RefreshTokensContainerName, PartitionKeyPath)
        {
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys =
                {
                    new UniqueKey { Paths = { UniqueTokenHashPath } }
                }
            }
        };

        await database.CreateContainerIfNotExistsAsync(
            refreshTokensContainerProperties,
            cancellationToken: cancellationToken);

        var emailIndexContainerProperties = new ContainerProperties(_options.EmailIndexContainerName, PartitionKeyPath);

        await database.CreateContainerIfNotExistsAsync(
            emailIndexContainerProperties,
            cancellationToken: cancellationToken);

        var externalLoginIndexContainerProperties = new ContainerProperties(_options.ExternalLoginIndexContainerName, PartitionKeyPath);

        await database.CreateContainerIfNotExistsAsync(
            externalLoginIndexContainerProperties,
            cancellationToken: cancellationToken);

        var userProfilesContainerProperties = new ContainerProperties(_options.UserProfilesContainerName, PartitionKeyPath)
        {
            IndexingPolicy = new IndexingPolicy
            {
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/updatedAt", Order = CompositePathSortOrder.Descending },
                        new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        };

        var userProfilesResponse = await database.CreateContainerIfNotExistsAsync(
            userProfilesContainerProperties,
            cancellationToken: cancellationToken);

        await EnsureUserProfileCompositeIndexesAsync(userProfilesResponse.Container, userProfilesResponse.Resource, cancellationToken);

        await EnsureFavouritesContainerOnDatabaseAsync(database, cancellationToken);

        var houseRulesContainerName = string.IsNullOrWhiteSpace(_options.HouseRulesContainerName)
            ? "RoomHouseRules"
            : _options.HouseRulesContainerName;
        await database.CreateContainerIfNotExistsAsync(houseRulesContainerName, HouseRulesPartitionKeyPath, cancellationToken: cancellationToken);

        var listingReportsContainerName = string.IsNullOrWhiteSpace(_options.ListingReportsContainerName)
            ? "ListingReports"
            : _options.ListingReportsContainerName;
        await database.CreateContainerIfNotExistsAsync(listingReportsContainerName, ListingReportsPartitionKeyPath, cancellationToken: cancellationToken);
        _favouritesContainerEnsured = true;

        var roomsContainerName = string.IsNullOrWhiteSpace(_options.RoomsContainerName) ? "Rooms" : _options.RoomsContainerName;
        var roomsContainerProperties = new ContainerProperties(roomsContainerName, PartitionKeyPath);
        roomsContainerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
        {
            Path = "/location/*",
            SpatialTypes = { SpatialType.Point }
        });
        roomsContainerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
        {
            Path = "/Location/*",
            SpatialTypes = { SpatialType.Point }
        });
        roomsContainerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
            new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
        });
        roomsContainerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/price", Order = CompositePathSortOrder.Descending },
            new() { Path = "/id", Order = CompositePathSortOrder.Descending }
        });
        roomsContainerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/createdAt", Order = CompositePathSortOrder.Descending },
            new() { Path = "/id", Order = CompositePathSortOrder.Descending }
        });

        var roomsResponse = await database.CreateContainerIfNotExistsAsync(
            roomsContainerProperties,
            cancellationToken: cancellationToken);

        await EnsureRoomsSpatialAndCompositeIndexesAsync(roomsResponse.Container, roomsResponse.Resource, cancellationToken);
    }

    private async Task EnsureFavouritesContainerOnDatabaseAsync(
        Database database,
        CancellationToken cancellationToken)
    {
        var favouritesContainerName = string.IsNullOrWhiteSpace(_options.FavouritesContainerName)
            ? "Favourites"
            : _options.FavouritesContainerName;

        var favouritesContainerProperties = new ContainerProperties(favouritesContainerName, FavouritesPartitionKeyPath)
        {
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys =
                {
                    new UniqueKey { Paths = { "/userId", "/roomId" } }
                }
            },
            IndexingPolicy = new IndexingPolicy
            {
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/createdAtUtc", Order = CompositePathSortOrder.Descending },
                        new CompositePath { Path = "/roomId", Order = CompositePathSortOrder.Ascending }
                    },
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/userId", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/createdAtUtc", Order = CompositePathSortOrder.Descending },
                        new CompositePath { Path = "/roomId", Order = CompositePathSortOrder.Ascending }
                    }
                }
            }
        };

        var response = await database.CreateContainerIfNotExistsAsync(
            favouritesContainerProperties,
            cancellationToken: cancellationToken);

        await EnsureFavouritesCompositeIndexesAsync(response.Container, response.Resource, cancellationToken);
    }

    private static async Task EnsureFavouritesCompositeIndexesAsync(
        Container container,
        ContainerProperties properties,
        CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;
        var modified = false;

        if (!HasCompositeIndex(indexingPolicy, "/userId", CompositePathSortOrder.Ascending, "/createdAtUtc", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new() { Path = "/userId", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/createdAtUtc", Order = CompositePathSortOrder.Descending },
                new() { Path = "/roomId", Order = CompositePathSortOrder.Ascending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/createdAtUtc", CompositePathSortOrder.Descending, "/roomId", CompositePathSortOrder.Ascending))
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new() { Path = "/createdAtUtc", Order = CompositePathSortOrder.Descending },
                new() { Path = "/roomId", Order = CompositePathSortOrder.Ascending }
            });
            modified = true;
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    private static async Task EnsureSpatialIndexesAsync(
        Container container,
        ContainerProperties properties,
        CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;

        var requiredPaths = new[] { "/location/*", "/Location/*" };
        var modified = false;

        foreach (var path in requiredPaths)
        {
            var existing = indexingPolicy.SpatialIndexes.FirstOrDefault(sp =>
                string.Equals(sp.Path, path, StringComparison.Ordinal));

            if (existing is null)
            {
                indexingPolicy.SpatialIndexes.Add(new SpatialPath
                {
                    Path = path,
                    SpatialTypes = { SpatialType.Point }
                });
                modified = true;
            }
            else if (!existing.SpatialTypes.Contains(SpatialType.Point))
            {
                existing.SpatialTypes.Add(SpatialType.Point);
                modified = true;
            }
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    private static async Task EnsureRoomsSpatialAndCompositeIndexesAsync(
        Container container,
        ContainerProperties properties,
        CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;

        var requiredPaths = new[] { "/location/*", "/Location/*" };
        var modified = false;

        foreach (var path in requiredPaths)
        {
            var existing = indexingPolicy.SpatialIndexes.FirstOrDefault(sp =>
                string.Equals(sp.Path, path, StringComparison.Ordinal));

            if (existing is null)
            {
                indexingPolicy.SpatialIndexes.Add(new SpatialPath
                {
                    Path = path,
                    SpatialTypes = { SpatialType.Point }
                });
                modified = true;
            }
            else if (!existing.SpatialTypes.Contains(SpatialType.Point))
            {
                existing.SpatialTypes.Add(SpatialType.Point);
                modified = true;
            }
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Ascending, "/id", CompositePathSortOrder.Ascending))
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/createdAt", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
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

    private static bool HasCompositeIndex(
        IndexingPolicy policy,
        string path1,
        CompositePathSortOrder order1,
        string path2,
        CompositePathSortOrder order2)
    {
        return policy.CompositeIndexes.Any(ci =>
            ci.Count >= 2 &&
            string.Equals(ci[0].Path, path1, StringComparison.Ordinal) &&
            ci[0].Order == order1 &&
            string.Equals(ci[1].Path, path2, StringComparison.Ordinal) &&
            ci[1].Order == order2);
    }

    private static async Task EnsureUserProfileCompositeIndexesAsync(
        Container container,
        ContainerProperties properties,
        CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;

        var hasCompositeIndex = indexingPolicy.CompositeIndexes.Any(compositeList =>
            compositeList.Count == 2 &&
            compositeList[0].Path.Equals("/updatedAt", StringComparison.OrdinalIgnoreCase) &&
            compositeList[0].Order == CompositePathSortOrder.Descending &&
            compositeList[1].Path.Equals("/id", StringComparison.OrdinalIgnoreCase) &&
            compositeList[1].Order == CompositePathSortOrder.Ascending);

        if (!hasCompositeIndex)
        {
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>
            {
                new CompositePath { Path = "/updatedAt", Order = CompositePathSortOrder.Descending },
                new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });

            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    public void Dispose()
    {
        _favouritesInitLock.Dispose();
        if (_ownsCosmosClient)
        {
            _cosmosClient?.Dispose();
        }
    }
}
