using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Binders;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Security;

var builder = WebApplication.CreateBuilder(args);

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

// ADR-012 / system-architecture §7.2: framework-generated 400s (model binding) must use the same
// problem+json shape as domain validation errors, with a stable errorCode and an `errors` map.
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ValidationProblemResponseFactory.Create);

// ===== 2. Datastore =====
// ADR-002: SQLite is the only datastore (the CosmosDb provider is frozen).
var sqliteSettings = builder.Configuration.GetSection(SqliteSettings.SectionName).Get<SqliteSettings>() ?? new SqliteSettings();
sqliteSettings.SeedDemoRooms = builder.Configuration.GetValue<bool?>("Sqlite:SeedDemoRooms")
    ?? builder.Environment.IsDevelopment(); // H4: demo seed only for local development
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

builder.Services.AddInfrastructure(jwtSettings, useMockSsoVerifier: useMockSso);

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

app.UseMiddleware<AuthExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "RoomToRentRadar.Api" }));

app.Run();

// Cho phép test project (WebApplicationFactory<Program>) tham chiếu entry point của API.
public partial class Program { }
