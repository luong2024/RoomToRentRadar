using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Api.Endpoints;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Services;

var builder = WebApplication.CreateBuilder(args);

// Shared SQLite database & DI contracts (falls back to In-Memory Fakes until backend tasks register real services)
var dbFile = Path.Combine(builder.Environment.ContentRootPath, Constants.DatabaseFilename);
builder.Services.AddRentalInfrastructure($"Data Source={dbFile}");

// T01.1 (Quang): real SQLite auth (overrides InMemoryAuthService) + JWT Bearer authentication.
// Endpoints must read the caller from the token ("sub"/"role"), never from IAuthService.CurrentUser.
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.AddJwtAuth();

var app = builder.Build();

app.UseJwtAuth(); // T01.1: authentication + authorization middleware, before endpoints are mapped

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
