using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Bootstrap.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationUseCases()
    .AddOutboundAdapters(builder.Configuration, builder.Environment)
    .AddRoomDetailServices(builder.Configuration)
    .AddInboundAdapters(builder.Configuration)
    .AddProfileFeature()
    .AddFavouritesFeature()
    .AddListingReportServices();

var app = builder.Build();

app.UseExceptionHandler();
// Android emulators access the development host through 10.0.2.2. Redirecting
// HTTP to the localhost development certificate changes that host and breaks the
// TLS handshake. Keep HTTPS mandatory outside Development, while allowing the
// explicitly configured HTTP development endpoint (port 49966).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var cosmosSettings = scope.ServiceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    try
    {
        var cosmosDbContext = scope.ServiceProvider.GetRequiredService<ICosmosDbContext>();
        await cosmosDbContext.EnsureDatabaseAndContainersAsync();

        if (!string.Equals(cosmosSettings.AccountEndpoint, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            var initializer = scope.ServiceProvider.GetRequiredService<CosmosDbInitializer>();
            await initializer.InitializeAsync();
        }
    }
    catch (Exception ex) when (app.Environment.IsDevelopment() && IsCosmosEmulatorConnectionException(ex))
    {
        app.Logger.LogWarning(ex, "Could not connect to Cosmos DB Emulator at startup in Development. Ensure Cosmos Emulator is running if working with real database.");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Cosmos DB initialization encountered a critical failure in {EnvironmentName}. Failing fast: {Message}", app.Environment.EnvironmentName, ex.Message);
        throw;
    }
}

await app.RunAsync();

static bool IsCosmosEmulatorConnectionException(Exception ex)
{
    return ex is HttpRequestException ||
           ex is System.Net.Sockets.SocketException ||
           ex.InnerException is HttpRequestException ||
           ex.InnerException is System.Net.Sockets.SocketException ||
           (ex is CosmosException ce && (ce.StatusCode == 0 || (int)ce.StatusCode >= 500)) ||
           ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;
