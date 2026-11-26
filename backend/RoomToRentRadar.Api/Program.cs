using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Api.Endpoints;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Data;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using RoomToRentRadar.Services;

var builder = WebApplication.CreateBuilder(args);

// Shared SQLite database & DI contracts (falls back to In-Memory Fakes until backend tasks register real services)
// "Database:Path" (env Database__Path) lets Docker keep the file on a volume, e.g. /app/data/AppSQLite.db3.
var dbFile = builder.Configuration["Database:Path"] is { Length: > 0 } configuredPath
    ? configuredPath
    : Path.Combine(builder.Environment.ContentRootPath, Constants.DatabaseFilename);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbFile))!);
builder.Services.AddRentalInfrastructure($"Data Source={dbFile}");

// MVC Web API: controllers in Controllers/, models in Models/.
// Invalid request bodies get the same Vietnamese ErrorResponse shape as every other error.
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Dữ liệu gửi lên không hợp lệ." : e.ErrorMessage)
                        .ToArray());

            var firstMessage = errors.Values.SelectMany(m => m).FirstOrDefault() ?? "Dữ liệu gửi lên không hợp lệ.";
            return new BadRequestObjectResult(new ErrorResponse(firstMessage, errors));
        };
    });

// T01.1 (Quang): real SQLite auth (overrides InMemoryAuthService) + JWT Bearer authentication.
// Endpoints must read the caller from the token ("sub"/"role"), never from IAuthService.CurrentUser.
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.AddJwtAuth();

// T02.1 (Thuy): real SQLite room listings (overrides InMemoryRoomListingRepository).
// Partial methods not yet implemented by T05.1/T09.2 still fall back to the in-memory base class.
builder.Services.AddSingleton<IRoomListingRepository, RoomListingRepository>();

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

// MVC controllers (Controllers/*Controller.cs). T01.1 Auth has moved here from Endpoints/AuthEndpoints.cs.
app.MapControllers();

// ============================================================================
// ISOLATED FEATURE ENDPOINT MODULES (LOCKED COMPOSITION ROOT)
// Legacy Minimal API endpoints; each task migrates its file to Controllers/<Feature>Controller.cs.
// ============================================================================
app.MapPhotoGalleryEndpoints();
app.MapRoomSearchEndpoints();
app.MapRoomPublishEndpoints();
app.MapFavoriteEndpoints();
app.MapAlertEndpoints();
app.MapHouseRuleEndpoints();
app.MapReportEndpoints();

app.Run();

public partial class Program { }
