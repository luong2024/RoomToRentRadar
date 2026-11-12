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

    public const string PartitionKeyPath = "/id";
    public const string UniqueEmailPath = "/normalizedEmail";
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
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.AccountEndpoint))
            {
                throw new InvalidOperationException(
                    "Azure Cosmos DB is not configured. 'CosmosDb:AccountEndpoint' is required. " +
                    "Set the endpoint in configuration or use CosmosDbContext.CreateInMemory() for local testing.");
            }

            if (string.IsNullOrWhiteSpace(options.AuthKeyOrResourceToken))
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

            _cosmosClient = new CosmosClient(options.AccountEndpoint, options.AuthKeyOrResourceToken, clientOptions);
            var usersCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.UsersContainerName);
            var refreshTokensCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.RefreshTokensContainerName);
            var emailIndexCosmosContainer = _cosmosClient.GetContainer(options.DatabaseName, options.EmailIndexContainerName);

            _usersContainer = new CosmosContainer<UserDocument>(usersCosmosContainer);
            _refreshTokensContainer = new CosmosContainer<RefreshTokenDocument>(refreshTokensCosmosContainer);
            _emailIndexContainer = new CosmosContainer<EmailIndexDocument>(emailIndexCosmosContainer);
        }
    }

    public ICosmosContainer<UserDocument> Users => _usersContainer;

    public ICosmosContainer<RefreshTokenDocument> RefreshTokens => _refreshTokensContainer;

    public ICosmosContainer<EmailIndexDocument> EmailIndex => _emailIndexContainer;

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

        var usersContainerProperties = new ContainerProperties(_options.UsersContainerName, PartitionKeyPath)
        {
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys =
                {
                    new UniqueKey { Paths = { UniqueEmailPath } }
                }
            }
        };

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
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
