using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Api.Seed;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Infrastructure.Email;
using RoomToRentRadar.Infrastructure.Photos;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.SqliteDataAccess.Seed;
using RoomToRentRadar.UseCases;

var builder = WebApplication.CreateBuilder(args);

var connectionString = SqliteDatabaseSetup.NormalizeConnectionString(
    builder.Configuration,
    builder.Environment.ContentRootPath);

var authTokenOptions = AuthTokenOptions.Resolve(
    builder.Configuration,
    builder.Environment.IsDevelopment());

var smtpEmailOptions = SmtpEmailOptions.Resolve(
    builder.Configuration,
    builder.Environment.IsDevelopment());

builder.Services
    .AddSingleton(authTokenOptions)
    .AddSingleton(smtpEmailOptions)
    .AddDataAccess(connectionString)
    .AddUseCases()
    .AddInfrastructure(builder.Configuration)
    .AddApiControllers()
    .AddListingsOpenApi()
    .AddDevelopmentCors()
    .AddMediaCors()
    .AddReverseProxySupport(builder.Configuration)
    .AddBearerAuthentication(authTokenOptions)
    .AddTravelRateLimiting()
    .AddAuthRateLimiting(builder.Configuration);

builder.Services.AddMemoryCache(cache =>
{
    cache.SizeLimit = 10_000;
    cache.CompactionPercentage = 0.1;
});

builder.Services.AddSingleton<SeedMediaWriter>();

var app = builder.Build();

app.UseForwardedHeaders();

if (string.IsNullOrWhiteSpace(app.Configuration["Travel:GoogleRoutesApiKey"]))
{
    app.Logger.LogWarning(
        "Travel:GoogleRoutesApiKey is not configured; travel estimates fall back to straight-line distances. " +
        "Configure it with 'dotnet user-secrets set \"Travel:GoogleRoutesApiKey\" \"<key>\" --project RoomToRentRadar.Api' " +
        "or the Travel__GoogleRoutesApiKey environment variable.");
}

if (authTokenOptions.IsEphemeralSigningKey)
{
    app.Logger.LogWarning(
        "Auth:Token:SigningKey is not configured; using a per-process development key, so tokens are " +
        "invalidated on restart. Configure it with 'dotnet user-secrets set \"Auth:Token:SigningKey\" " +
        "\"<secret>\" --project RoomToRentRadar.Api' or the Auth__Token__SigningKey environment variable.");
}

if (!smtpEmailOptions.IsConfigured)
{
    app.Logger.LogWarning(
        "Auth:Email:Smtp:Host is not configured; registration OTP emails are written to {OutboxDirectory}. " +
        "Configure Auth:Email:Smtp with user-secrets or environment variables to send real email.",
        FileEmailSender.ResolveOutboxDirectory(smtpEmailOptions, app.Environment.ContentRootPath));
}

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
    await scope.ServiceProvider.GetRequiredService<SeedMediaWriter>().EnsureSampleMediaAsync(dbContext);
    await DemoUserSeeder.SeedAsync(
        dbContext,
        app.Configuration,
        app.Services.GetRequiredService<IPasswordHasher>(),
        app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors(HostingExtensions.DevelopmentCorsPolicy);
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "RoomToRentRadar API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseWhen(
    context => context.Request.Path.StartsWithSegments(PhotoStorage.RequestPath),
    branch => branch.UseCors(HostingExtensions.MediaCorsPolicy));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        app.Services.GetRequiredService<PhotoStorageOptions>().RootPath),
    RequestPath = PhotoStorage.RequestPath,
    OnPrepareResponse = staticFileResponseContext =>
    {
        staticFileResponseContext.Context.Response.Headers.CacheControl =
            "public, max-age=31536000, immutable";
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program;
