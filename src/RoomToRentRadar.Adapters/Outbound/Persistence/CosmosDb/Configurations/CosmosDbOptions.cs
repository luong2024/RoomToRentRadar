namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;

public sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string AccountEndpoint { get; set; } = string.Empty;

    public string? AuthKey
    {
        get => AuthKeyOrResourceToken;
        set => AuthKeyOrResourceToken = value;
    }

    public string? AuthKeyOrResourceToken { get; set; }

    public string DatabaseName { get; set; } = "RoomToRentRadarDb";

    public string RoomDetailsContainerName { get; set; } = "RoomDetails";

    public string CampusesContainerName { get; set; } = "Campuses";

    public string RoomProjectionSyncOutboxContainerName { get; set; } = "RoomProjectionSyncOutbox";

    public string UsersContainerName { get; set; } = "Users";

    public string RefreshTokensContainerName { get; set; } = "RefreshTokens";

    public string EmailIndexContainerName { get; set; } = "EmailIndex";

    public string ExternalLoginIndexContainerName { get; set; } = "ExternalLoginIndex";

    public string UserProfilesContainerName { get; set; } = "UserProfiles";

    private string _roomsContainerName = "Rooms";
    private string? _containerName;

    public string RoomsContainerName
    {
        get => _roomsContainerName;
        set => _roomsContainerName = value;
    }

    public string ContainerName
    {
        get => _containerName ?? (_roomsContainerName != "Rooms" ? _roomsContainerName : "RoomListings");
        set
        {
            _containerName = value;
            if (_roomsContainerName == "Rooms" && value != "RoomListings")
            {
                _roomsContainerName = value;
            }
        }
    }

    public string ConnectionMode { get; set; } = "Direct";

    public bool IsEmulator { get; set; }

    public bool DisableTlsValidation { get; set; } = false;

    /// <summary>
    /// Whether to automatically seed initial sample room data during startup.
    /// Strictly defaults to false to prevent seeding test/demo data in production.
    /// </summary>
    public bool SeedSampleData { get; set; } = false;
}
