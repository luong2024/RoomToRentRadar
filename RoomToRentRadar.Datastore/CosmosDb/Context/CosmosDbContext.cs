using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;

namespace RoomToRentRadar.Datastore.CosmosDb.Context;

public sealed class CosmosDbContext : IDisposable
{
    private readonly CosmosClient? _client;
    private readonly CosmosDbSettings _settings;

    public const string UsersPartitionKeyPath = "/id";
    public const string UsersUniqueEmailPath = "/normalizedEmail";

    public bool IsConfigured => _client != null;

    public CosmosDbContext(CosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        if (!string.IsNullOrWhiteSpace(settings.EndpointUri) &&
            !string.IsNullOrWhiteSpace(settings.PrimaryKey))
        {
            _client = new CosmosClient(settings.EndpointUri, settings.PrimaryKey);
        }
    }

    public Container GetUsersContainer()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("CosmosClient is not configured with EndpointUri and PrimaryKey.");
        }

        return _client.GetContainer(_settings.DatabaseName, _settings.UsersContainerName);
    }

    public static ContainerProperties BuildUsersContainerProperties(string containerName)
    {
        var uniqueKeyPolicy = new UniqueKeyPolicy
        {
            UniqueKeys =
            {
                new UniqueKey
                {
                    Paths = { UsersUniqueEmailPath }
                }
            }
        };

        return new ContainerProperties(containerName, UsersPartitionKeyPath)
        {
            UniqueKeyPolicy = uniqueKeyPolicy
        };
    }

    public async Task EnsureUsersContainerAsync(CancellationToken ct = default)
    {
        if (_client == null)
        {
            return;
        }

        var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(
            _settings.DatabaseName,
            cancellationToken: ct);

        var containerProperties = BuildUsersContainerProperties(_settings.UsersContainerName);
        await dbResponse.Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            cancellationToken: ct);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

