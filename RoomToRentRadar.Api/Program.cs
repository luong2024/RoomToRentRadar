using System.Globalization;
using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Data.Seed;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Features.Favourites;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Moderation;
using RoomToRentRadar.Api.Features.Notifications;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Api.Features.Reports;
using RoomToRentRadar.Api.Features.SavedSearches;
using RoomToRentRadar.Api.Features.Travel;
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

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "opaque",
                Description = "Send the access token returned by POST /api/v1/auth/login."
            }
        };

        foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
        {
            if (!RequiresAuthorization(description)
                || string.IsNullOrEmpty(description.RelativePath)
                || description.HttpMethod is null)
            {
                continue;
            }

            var path = "/" + description.RelativePath.TrimStart('/');
            document.Paths.TryGetValue(path, out var pathItem);
            if (pathItem is null)
            {
                continue;
            }

            var operations = pathItem.Operations;
            if (operations is null)
            {
                continue;
            }

            operations.TryGetValue(new HttpMethod(description.HttpMethod), out var operation);
            if (operation is null)
            {
                continue;
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
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
builder.Services.AddScoped<ListingManagementService>();
builder.Services.AddScoped<AuthService>();
builder.Services
    .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ModeratorOrAdmin, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(UserRoleWire.Moderator, UserRoleWire.Admin));
});
builder.Services.AddSingleton(serviceProvider => PhotoStorageOptions.Resolve(
    serviceProvider.GetRequiredService<IConfiguration>()[PhotoStorageOptions.ConfigKey],
    serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath));
builder.Services.AddSingleton<PhotoStorage>();
builder.Services.AddSingleton<PhotoProcessor>();
builder.Services.AddSingleton<ListingPhotoWriter>();
builder.Services.AddScoped<ListingPhotosService>();
builder.Services.AddScoped<AreaSearchQueryService>();
builder.Services.Configure<AlertsOptions>(
    builder.Configuration.GetSection(AlertsOptions.SectionName));
builder.Services.AddScoped<SavedSearchService>();
builder.Services.AddScoped<SavedSearchAlertEvaluator>();
builder.Services.AddScoped<NotificationsService>();
builder.Services.AddScoped<FavouriteService>();
builder.Services.AddScoped<ListingReportsService>();
builder.Services.AddScoped<ModerationQueueService>();
builder.Services.AddScoped<ModerationActionsService>();
builder.Services.AddHostedService<SavedSearchAlertWorker>();
builder.Services.AddMemoryCache(cache =>
{
    cache.SizeLimit = 10_000;
    cache.CompactionPercentage = 0.1;
});
builder.Services.Configure<TravelOptions>(
    builder.Configuration.GetSection(TravelOptions.SectionName));
builder.Services
    .AddHttpClient<IRoutingEstimatesProvider, GoogleRoutesEstimatesProvider>(client =>
    {
        client.BaseAddress = new Uri("https://routes.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddStandardResilienceHandler(resilience =>
    {
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        resilience.Retry.MaxRetryAttempts = 1;
    });
builder.Services.AddScoped<TravelEstimatesService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Proxies are only trusted when explicitly configured; loopback stays trusted by default.
    var section = builder.Configuration.GetSection("ForwardedHeaders");

    foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }

    foreach (var network in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
    {
        if (System.Net.IPNetwork.TryParse(network, out var parsed))
        {
            options.KnownIPNetworks.Add(parsed);
        }
    }
});
builder.Services.AddRateLimiter(rateLimiter =>
{
    rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiter.AddPolicy(TravelRateLimitPolicy.Name, context =>
    {
        var travelOptions = context.RequestServices
            .GetRequiredService<IOptions<TravelOptions>>()
            .Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, travelOptions.RateLimitPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    rateLimiter.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var problem = TravelProblemFactory.RateLimitedProblem();
        context.HttpContext.Response.StatusCode = problem.Status!.Value;
        await context.HttpContext.Response.WriteAsJsonAsync(
            problem,
            ApiJson.Options,
            "application/problem+json",
            cancellationToken);
    };
});

var app = builder.Build();

app.UseForwardedHeaders();

if (string.IsNullOrWhiteSpace(app.Configuration["Travel:GoogleRoutesApiKey"]))
{
    app.Logger.LogWarning(
        "Travel:GoogleRoutesApiKey is not configured; travel estimates fall back to straight-line distances. " +
        "Configure it with 'dotnet user-secrets set \"Travel:GoogleRoutesApiKey\" \"<key>\" --project RoomToRentRadar.Api' " +
        "or the Travel__GoogleRoutesApiKey environment variable.");
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
app.UseRateLimiter();

app.MapControllers();

app.Run();

static bool RequiresAuthorization(ApiDescription description)
{
    if (description.ActionDescriptor is not ControllerActionDescriptor action)
    {
        return false;
    }

    return action.MethodInfo.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()
        || action.ControllerTypeInfo.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any();
}

public partial class Program;
