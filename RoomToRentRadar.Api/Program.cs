using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Data.Seed;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);
const string DevelopmentCorsPolicy = "Development";

var connectionString = SqliteDatabaseSetup.NormalizeConnectionString(
    builder.Configuration,
    builder.Environment.ContentRootPath);

builder.Services.AddSingleton<SqlitePragmaInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlite(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<SqlitePragmaInterceptor>());
    options.AddInterceptors(serviceProvider.GetServices<DbCommandInterceptor>());
});

builder.Services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
builder.Services.AddControllers()
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ =>
    {
        var problem = ListingProblemFactory.InvalidQuery(
            ListingProblemFactory.InvalidParameterFormat,
            "One or more request parameters have an invalid format.");
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, _) =>
    {
        var request = context.ApplicationServices
            .GetRequiredService<IHttpContextAccessor>()
            .HttpContext?.Request;

        if (request is not null)
        {
            document.Servers = new List<OpenApiServer>
            {
                new() { Url = $"{request.Scheme}://{request.Host}{request.PathBase}" }
            };
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevelopmentCorsPolicy, policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddScoped<ListingsQueryService>();
builder.Services.AddScoped<ListingCandidatePageBuilder>();
builder.Services.AddScoped<AuthService>();
builder.Services
    .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(serviceProvider => PhotoStorageOptions.Resolve(
    serviceProvider.GetRequiredService<IConfiguration>()[PhotoStorageOptions.ConfigKey],
    serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath));
builder.Services.AddSingleton<PhotoStorage>();
builder.Services.AddSingleton<PhotoProcessor>();
builder.Services.AddScoped<ListingPhotosService>();
builder.Services.AddScoped<AreaSearchQueryService>();

var app = builder.Build();

await SqliteDatabaseSetup.EnableWalAsync(connectionString);

if (SeedCommand.IsSeedCommand(args))
{
    await using var seedScope = app.Services.CreateAsyncScope();
    var seedDbContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await seedDbContext.Database.MigrateAsync();
    Environment.ExitCode = await SeedCommand.ExecuteAsync(seedDbContext, args);
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedSampleAsync(dbContext);
    await DatabaseSeeder.BackfillSampleDetailsAsync(dbContext);
    await DemoUserSeeder.SeedAsync(dbContext, app.Configuration, app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "RoomToRentRadar API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        app.Services.GetRequiredService<PhotoStorageOptions>().RootPath),
    RequestPath = PhotoStorage.RequestPath
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
