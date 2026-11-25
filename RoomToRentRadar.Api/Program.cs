using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.BackgroundJobs;
using RoomToRentRadar.Api.Binders;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Api.Security;
using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Media;
using RoomToRentRadar.Infrastructure.Adapters.Routing;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

// ===== 1. Controllers & JSON =====
builder.Services.AddControllers(options =>
    {
        // US04: ?roomTypes=MiniApartment,WholeHouse (OpenAPI style: form, explode: false).
        options.ModelBinderProviders.Insert(0, new RoomTypesModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        // ADR-012: enums are serialized as strings ("Available", "MiniApartment"); numbers are still accepted on input.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContextPort, HttpContextUserContextAdapter>();

// ADR-012 / system-architecture §7.2: framework-generated 400s (model binding) must use the same
// problem+json shape as domain validation errors, with a stable errorCode and an `errors` map.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ValidationProblemResponseFactory.Create;
    // MVC-produced 4xx (e.g. 415, bare 404) must go through the status-code pages path to get a stable errorCode.
    options.SuppressMapClientErrors = true;
});

// ===== 2. Datastore =====
// ADR-002: SQLite is the only datastore (the CosmosDb provider is frozen).
var sqliteSettings = builder.Configuration.GetSection(SqliteSettings.SectionName).Get<SqliteSettings>() ?? new SqliteSettings();
sqliteSettings.SeedDemoRooms = builder.Configuration.GetValue<bool?>("Sqlite:SeedDemoRooms")
    ?? builder.Environment.IsDevelopment(); // H4: demo seed only for local development
sqliteSettings.SeedExtendedDemoData = builder.Configuration.GetValue<bool?>("Sqlite:SeedExtendedDemoData")
    ?? builder.Environment.IsDevelopment();
sqliteSettings.SeedAccounts = SeedAccountsLoader.Load(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    new Pbkdf2PasswordHasherAdapter());
// T00.10: demo photos exist in Development only and are served straight from the committed folder.
var seedMediaPath = Path.Combine(builder.Environment.ContentRootPath, SeedPhotoLoader.FolderName);
var seedPhotosRequested = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DemoData:SeedPhotos");
// The images stay in the project folder; a run from the build output has none and simply seeds no photos.
var seedDemoPhotos = seedPhotosRequested
    && Directory.Exists(Path.Combine(seedMediaPath, SeedPhotoLoader.ImagesFolderName));
if (seedDemoPhotos)
{
    sqliteSettings.SeedPhotos = SeedPhotoLoader.Load(seedMediaPath);
}

builder.Services.AddSqliteDatastore(sqliteSettings);

// ===== 3. Application =====
builder.Services.AddApplicationServices();

// ===== 4. Infrastructure =====
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("CRITICAL: JWT SigningKey must be configured in Production/Staging environments.");
    }

    Console.WriteLine("DEVELOPMENT WARNING: No JWT SigningKey was configured. Generating ephemeral in-memory signing key.");
    jwtSettings.SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

var useMockSso = builder.Configuration.GetValue("IctuSso:UseMockVerifier", defaultValue: false);
if (useMockSso && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("CRITICAL: Mock SSO verifier is strictly prohibited outside the Development environment.");
}

var mediaSettings = builder.Configuration.GetSection(MediaSettings.SectionName).Get<MediaSettings>()
    ?? new MediaSettings();

var routingSettings = builder.Configuration.GetSection(RoutingSettings.SectionName).Get<RoutingSettings>()
    ?? new RoutingSettings();
var useMockRouting = builder.Configuration.GetValue("Routing:UseMock", defaultValue: false);
if (useMockRouting && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("CRITICAL: Mock routing adapter is strictly prohibited outside the Development environment.");
}

if (!useMockRouting && string.IsNullOrWhiteSpace(routingSettings.OpenRouteServiceApiKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("CRITICAL: Routing:OpenRouteServiceApiKey must be configured in Production/Staging environments.");
    }

    Console.WriteLine("DEVELOPMENT WARNING: No Routing:OpenRouteServiceApiKey configured; routing requests will return straight-line fallback.");
}

if (!useMockRouting)
{
    builder.Services.AddHttpClient<ITravelTimePort, OpenRouteServiceTravelTimeAdapter>();
}

builder.Services.AddInfrastructure(
    jwtSettings,
    useMockSsoVerifier: useMockSso,
    mediaSettings: mediaSettings,
    useMockRouting: useMockRouting,
    routingSettings: routingSettings);

// BR-MED-03: hourly sweep of uploads that were never attached to a listing.
builder.Services.AddHostedService<PhotoCleanupService>();

// ===== 5. Authentication & authorization =====
var signingKeyBytes = Encoding.UTF8.GetBytes(jwtSettings.SigningKey);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.ConfigurePolicies);

// ===== 6. Request pipeline =====
var app = builder.Build();

if (seedPhotosRequested && !seedDemoPhotos)
{
    app.Logger.LogWarning(
        "DemoData:SeedPhotos is on but '{SeedMediaPath}' has no images; demo rooms get no photos. Run the API with 'dotnet run'.",
        seedMediaPath);
}

app.UseMiddleware<ProblemDetailsMiddleware>();
// 401/403/404/415 produced without a body (auth challenge, unknown route, unsupported media type) become problem+json with a stable errorCode.
app.UseStatusCodePages(StatusCodeProblemPages.WriteAsync);

if (seedDemoPhotos)
{
    // Only the images: the manifest and credits next to them are not published.
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(seedMediaPath, SeedPhotoLoader.ImagesFolderName)),
        RequestPath = SeedPhotoLoader.RequestPath
    });
}

// ADR-008 / system-architecture §6.1: uploaded photos are served as immutable, year-long cacheable files.
var mediaRootPath = Path.GetFullPath(
    string.IsNullOrWhiteSpace(mediaSettings.RootPath) ? "media" : mediaSettings.RootPath);
Directory.CreateDirectory(mediaRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRootPath),
    RequestPath = "/media",
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable"
});

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();

// Cho phép test project (WebApplicationFactory<Program>) tham chiếu entry point của API.
public partial class Program { }
