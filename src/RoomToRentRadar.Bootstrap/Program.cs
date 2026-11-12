using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Bootstrap.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationUseCases()
    .AddOutboundAdapters(builder.Configuration)
    .AddInboundAdapters();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetService<CosmosDbInitializer>();
    if (initializer is not null)
    {
        try
        {
            await initializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not initialize Cosmos DB at startup. Ensure Cosmos Emulator is running if working locally.");
        }
    }
}

app.Run();

public partial class Program;
