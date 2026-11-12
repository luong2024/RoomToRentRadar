using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Api.Security;
using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore.CosmosDb;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            };
            problemDetails.Extensions["errorCode"] = "VALIDATION_FAILED";
            return new BadRequestObjectResult(problemDetails);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContextPort, HttpContextUserContextAdapter>();

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

// ADR-002: Cosmos frozen, SQLite only for datastore
var sqliteSettings = builder.Configuration.GetSection(SqliteSettings.SectionName).Get<SqliteSettings>() ?? new SqliteSettings();
builder.Services.AddSqliteDatastore(sqliteSettings);

var useMockSso = builder.Configuration.GetValue("IctuSso:UseMockVerifier", defaultValue: false);
if (useMockSso && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("CRITICAL: Mock SSO verifier is strictly prohibited outside the Development environment.");
}

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(jwtSettings, useMockSsoVerifier: useMockSso);

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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();

public partial class Program { }
