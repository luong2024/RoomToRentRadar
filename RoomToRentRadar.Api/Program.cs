using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register Hexagonal Architecture Layers
builder.Services.AddApplicationServices();
builder.Services.AddSqliteDatastore();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();
