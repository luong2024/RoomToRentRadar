using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Cosmos DB ----
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection(CosmosDbOptions.SectionName));

// CosmosClient nên là Singleton (thread-safe, tái sử dụng kết nối).
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return new CosmosClient(o.Endpoint, o.Key, new CosmosClientOptions
    {
        ApplicationName = "RoomToRentRadar.Api",
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
});

// ---- Repository / Service ----
builder.Services.AddScoped<IListingRepository, CosmosListingRepository>();
builder.Services.AddScoped<IListingService, ListingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
