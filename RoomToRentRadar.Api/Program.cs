using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.BackgroundJobs;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Security;

var builder = WebApplication.CreateBuilder(args);

var mediaRootSetting = builder.Configuration["Media:RootPath"];
if (string.IsNullOrWhiteSpace(mediaRootSetting))
{
    mediaRootSetting = "uploads";
}

var mediaRoot = Path.Combine(builder.Environment.ContentRootPath, mediaRootSetting);
Directory.CreateDirectory(mediaRoot);

var mediaPublicBaseUrl = builder.Configuration["Media:PublicBaseUrl"];
if (string.IsNullOrWhiteSpace(mediaPublicBaseUrl))
{
    // Fail fast: public URLs must be explicit outside Development.
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("CRITICAL: Media:PublicBaseUrl must be configured outside the Development environment.");
    }

    mediaPublicBaseUrl = "http://localhost:5000";
}

// Enums are serialized as strings (AGENTS.md §6, openapi.yaml PhotoKind: type string).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        ApiProblemFactory.ToResult(ApiProblemFactory.FromModelState(context));
});

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

var sqliteSettings = builder.Configuration.GetSection(SqliteSettings.SectionName).Get<SqliteSettings>() ?? new SqliteSettings();
builder.Services.AddSqliteDatastore(sqliteSettings);

var useMockSso = builder.Configuration.GetValue("IctuSso:UseMockVerifier", defaultValue: false);
if (useMockSso && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("CRITICAL: Mock SSO verifier is strictly prohibited outside the Development environment.");
}

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(jwtSettings, useMockSsoVerifier: useMockSso);
builder.Services.AddPhotoInfrastructure(mediaRoot, mediaPublicBaseUrl);
builder.Services.AddHostedService<PhotoCleanupService>();

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

var app = builder.Build();

app.UseMiddleware<AuthExceptionHandlingMiddleware>();
app.UseStatusCodePages(async (StatusCodeContext statusCodeContext) =>
{
    var response = statusCodeContext.HttpContext.Response;
    var problem = ApiProblemFactory.FromStatusCode(
        response.StatusCode,
        statusCodeContext.HttpContext.Request.Path);

    if (problem is null)
    {
        return;
    }

    response.ContentType = ApiProblemFactory.ProblemContentType;
    await JsonSerializer.SerializeAsync(
        response.Body,
        problem,
        cancellationToken: statusCodeContext.HttpContext.RequestAborted);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
    OnPrepareResponse = ctx =>
    {
        // Photos are immutable (server-generated names); cache them for a year.
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    }
});

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();

public partial class Program
{
}
