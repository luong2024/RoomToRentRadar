using RoomToRentRadar.Api;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Landlord;
using RoomToRentRadar.Api.Services.Favourites;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Api.Services.SavedSearches;

var builder = WebApplication.CreateBuilder(args);

// Factory bắt buộc: nếu để DI tự chọn constructor, nó sẽ chọn overload (IEnumerable<ListingDocument>)
// và inject một danh sách RỖNG => API chạy không có dữ liệu seed.
builder.Services.AddSingleton(_ => new InMemoryListingRepository());
builder.Services.AddSingleton<IListingRepository>(sp => sp.GetRequiredService<InMemoryListingRepository>());
builder.Services.AddSingleton<ILandlordListingRepository>(sp => sp.GetRequiredService<InMemoryListingRepository>());
builder.Services.AddSingleton<IFavouriteRepository, InMemoryFavouriteRepository>();
builder.Services.AddSingleton<IModerationAuditRepository, InMemoryModerationAuditRepository>();
builder.Services.AddScoped<IListingService, ListingService>();
builder.Services.AddScoped<IListingDetailService, ListingDetailService>();
builder.Services.AddScoped<IListingSearchService, ListingSearchService>();
builder.Services.AddScoped<IFavouriteService, FavouriteService>();
builder.Services.AddScoped<IListingModerationService, ListingModerationService>();
builder.Services.AddScoped<ISpatialSearchService, SpatialSearchService>();
builder.Services.AddPhotoPipeline(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISavedSearchRepository, InMemorySavedSearchRepository>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
builder.Services.AddScoped<ISavedSearchAlertRules, SavedSearchAlertRules>();
builder.Services.AddScoped<ILandlordListingService, LandlordListingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml))
    {
        options.IncludeXmlComments(xml);
    }
});

var app = builder.Build();

app.UsePhotoStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

public partial class Program;
