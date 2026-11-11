using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Photos;

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

// ---- Photo upload pipeline ----
builder.Services.Configure<PhotoOptions>(builder.Configuration.GetSection(PhotoOptions.SectionName));
builder.Services.AddScoped<IPhotoRepository, CosmosPhotoRepository>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
// Đổi sang AzureBlobPhotoStorage ở đây khi chuyển lên cloud.
builder.Services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();

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

// Phục vụ ảnh đã upload qua URL công khai (ví dụ /uploads/rooms/{roomId}/{id}.jpg).
var photoOpt = app.Services.GetRequiredService<IOptions<PhotoOptions>>().Value;
var uploadRoot = Path.GetFullPath(Path.IsPathRooted(photoOpt.StorageRoot)
    ? photoOpt.StorageRoot
    : Path.Combine(app.Environment.ContentRootPath, photoOpt.StorageRoot));
Directory.CreateDirectory(uploadRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadRoot),
    RequestPath = "/" + photoOpt.RequestPath.Trim('/')
});
app.MapControllers();

app.Run();
