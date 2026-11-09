var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();

