namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;

public sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string AccountEndpoint { get; set; } = string.Empty;

    public string? AuthKey { get; set; }

    public string? AuthKeyOrResourceToken { get; set; }

    public string DatabaseName { get; set; } = "RoomToRentRadarDb";

    public string UsersContainerName { get; set; } = "Users";

    public string RefreshTokensContainerName { get; set; } = "RefreshTokens";

    public string EmailIndexContainerName { get; set; } = "EmailIndex";

    public string ExternalLoginIndexContainerName { get; set; } = "ExternalLoginIndex";

    public string ConnectionMode { get; set; } = "Direct";
}
