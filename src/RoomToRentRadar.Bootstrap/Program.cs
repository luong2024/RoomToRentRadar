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

app.Run();

public partial class Program;
