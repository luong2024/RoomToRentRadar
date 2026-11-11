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

        _isInMemory = string.IsNullOrWhiteSpace(options.AccountEndpoint) ||
                      string.Equals(options.AccountEndpoint, "in-memory", StringComparison.OrdinalIgnoreCase) ||
                      string.IsNullOrWhiteSpace(options.AuthKeyOrResourceToken);

        if (_isInMemory)
        {
            _usersContainer = new InMemoryCosmosContainer<UserDocument>();
            _refreshTokensContainer = new InMemoryCosmosContainer<RefreshTokenDocument>();
        }
        else
        {
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

            _usersContainer = new CosmosContainer<UserDocument>(usersCosmosContainer);
            _refreshTokensContainer = new CosmosContainer<RefreshTokenDocument>(refreshTokensCosmosContainer);
        }
    }

    public ICosmosContainer<UserDocument> Users => _usersContainer;

    public ICosmosContainer<RefreshTokenDocument> RefreshTokens => _refreshTokensContainer;

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
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
