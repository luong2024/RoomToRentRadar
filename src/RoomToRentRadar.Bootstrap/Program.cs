using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Bootstrap.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationUseCases()
    .AddOutboundAdapters(builder.Configuration)
    .AddInboundAdapters(builder.Configuration);

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
}

await app.RunAsync();

public partial class Program;
