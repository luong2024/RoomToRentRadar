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

    public string UsersContainerName { get; set; } = "Users";

    public string RefreshTokensContainerName { get; set; } = "RefreshTokens";

    public string EmailIndexContainerName { get; set; } = "EmailIndex";

    public string ExternalLoginIndexContainerName { get; set; } = "ExternalLoginIndex";

    public string UserProfilesContainerName { get; set; } = "UserProfiles";

    public string RoomsContainerName { get; set; } = "Rooms";

    public string ContainerName
    {
        get => RoomsContainerName;
        set => RoomsContainerName = value;
    }

    public string ConnectionMode { get; set; } = "Direct";
}
