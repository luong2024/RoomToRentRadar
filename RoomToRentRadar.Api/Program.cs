using RoomToRentRadar;
using RoomToRentRadar.Api.Endpoints;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Shared SQLite database & DI contracts (falls back to In-Memory Fakes until backend tasks register real services)
var dbFile = Path.Combine(builder.Environment.ContentRootPath, Constants.DatabaseFilename);
builder.Services.AddRentalInfrastructure($"Data Source={dbFile}");

var app = builder.Build();

// Baseline health-check endpoint (verifies Web API & SQLite connectivity)
app.MapGet("/api/health", async (RentalDatabaseService db) =>
{
    await db.EnsureInitializedAsync();
    return Results.Ok(new
    {
        Status = "Healthy",
        Service = "RoomToRentRadar.Api (Group 5 - ICTU)",
        Campus = Constants.IctuCampusName
    });
});

// ============================================================================
// ISOLATED FEATURE ENDPOINT MODULES (LOCKED COMPOSITION ROOT)
// Do NOT edit Program.cs — implement your endpoints inside Endpoints/<Task>Endpoints.cs!
// ============================================================================
app.MapAuthEndpoints();
app.MapRoomListingEndpoints();
app.MapRoomDetailEndpoints();
app.MapPhotoGalleryEndpoints();
app.MapRoomSearchEndpoints();
app.MapRoomPublishEndpoints();
app.MapFavoriteEndpoints();
app.MapAlertEndpoints();
app.MapHouseRuleEndpoints();
app.MapReportEndpoints();

app.Run();

public partial class Program { }
