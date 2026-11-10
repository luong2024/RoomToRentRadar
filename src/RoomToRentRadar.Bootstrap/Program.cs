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

app.Run();

public partial class Program;
