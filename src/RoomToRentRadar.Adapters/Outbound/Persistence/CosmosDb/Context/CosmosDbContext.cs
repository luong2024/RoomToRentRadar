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

    private readonly ICosmosContainer<UserDocument> _usersContainer;
    private readonly ICosmosContainer<RefreshTokenDocument> _refreshTokensContainer;
    private readonly ICosmosContainer<EmailIndexDocument> _emailIndexContainer;
    private readonly ICosmosContainer<ExternalLoginIndexDocument> _externalLoginIndexContainer;
    private readonly ICosmosContainer<UserProfileDocument> _userProfilesContainer;

    public const string PartitionKeyPath = "/id";
    public const string UniqueTokenHashPath = "/tokenHash";

    public CosmosDbContext(IOptions<CosmosDbOptions> options)
        : this(options.Value)
    {
    }

    public CosmosDbContext(CosmosDbOptions options)
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

            if (string.Equals(options.ConnectionMode, "Gateway", StringComparison.OrdinalIgnoreCase))
            {
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
            }

            var authKey = !string.IsNullOrWhiteSpace(options.AuthKey)
                ? options.AuthKey
                : options.AuthKeyOrResourceToken;

            _cosmosClient = new CosmosClient(options.AccountEndpoint, authKey, clientOptions);
            var usersCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.UsersContainerName);
            var refreshTokensCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.RefreshTokensContainerName);
            var emailIndexCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.EmailIndexContainerName);
            var externalLoginIndexCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.ExternalLoginIndexContainerName);
            var userProfilesCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.UserProfilesContainerName);

            _usersContainer = new CosmosContainer<UserDocument>(usersCosmosContainer);
            _refreshTokensContainer = new CosmosContainer<RefreshTokenDocument>(refreshTokensCosmosContainer);
            _emailIndexContainer = new CosmosContainer<EmailIndexDocument>(emailIndexCosmosContainer);
            _externalLoginIndexContainer = new CosmosContainer<ExternalLoginIndexDocument>(externalLoginIndexCosmosContainer);
            _userProfilesContainer = new CosmosContainer<UserProfileDocument>(userProfilesCosmosContainer);
        }
    }

    public ICosmosContainer<UserDocument> Users => _usersContainer;

    public ICosmosContainer<RefreshTokenDocument> RefreshTokens => _refreshTokensContainer;

    public ICosmosContainer<EmailIndexDocument> EmailIndex => _emailIndexContainer;

    public ICosmosContainer<ExternalLoginIndexDocument> ExternalLoginIndex => _externalLoginIndexContainer;

    public ICosmosContainer<UserProfileDocument> UserProfiles => _userProfilesContainer;

    public static CosmosDbContext CreateInMemory()
    {
        return new CosmosDbContext(new CosmosDbOptions
        {
            AccountEndpoint = "in-memory",
            DatabaseName = "RoomToRentRadarDb"
        });
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

        var roomsContainerName = string.IsNullOrWhiteSpace(_options.ContainerName) ? "Rooms" : _options.ContainerName;
        await database.CreateContainerIfNotExistsAsync(roomsContainerName, PartitionKeyPath, cancellationToken: cancellationToken);

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
        _cosmosClient?.Dispose();
    }
}
