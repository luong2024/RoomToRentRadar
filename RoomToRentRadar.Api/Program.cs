using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IListingRepository, InMemoryListingRepository>();
builder.Services.AddScoped<IListingDetailService, ListingDetailService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml))
    {
        options.IncludeXmlComments(xml);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

public partial class Program;
