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
    .AddProfileFeature();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var cosmosDbContext = scope.ServiceProvider.GetRequiredService<ICosmosDbContext>();
    await cosmosDbContext.EnsureDatabaseAndContainersAsync();

    var cosmosSettings = scope.ServiceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    if (!string.Equals(cosmosSettings.AccountEndpoint, "in-memory", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var initializer = scope.ServiceProvider.GetRequiredService<CosmosDbInitializer>();
            await initializer.InitializeAsync();
        }
        catch (CosmosException ex)
        {
            app.Logger.LogWarning(ex, "Could not initialize Cosmos DB at startup. Ensure Cosmos Emulator is running if working locally.");
        }
    }
}

await app.RunAsync();

public partial class Program;
