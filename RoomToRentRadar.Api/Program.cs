using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Dtos;
using RoomToRentRadar.Api.Entities;
using RoomToRentRadar.Api.Options;
using RoomToRentRadar.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database (SQLite file from ConnectionStrings:AuthDb)
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AuthDb")));

// ---------------------------------------------------------------------------
// ASP.NET Core Identity (users + roles, no cookies: this API only uses JWT)
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // AC1: only "at least 6 characters" is required.
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.User.RequireUniqueEmail = true;

        // Brute-force protection used by AuthController.Login.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>();

// ---------------------------------------------------------------------------
// JWT: options validated at startup so a missing/short SigningKey fails fast
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configure JwtBearer from the validated JwtOptions (AC3: verify the Bearer token on each request).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Keep claim names exactly as TokenService writes them ("sub", "role").
        bearer.MapInboundClaims = false;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
        };

        // AC4: friendly JSON instead of an empty 401/403 body.
        bearer.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var message = context.AuthenticateFailure is SecurityTokenExpiredException
                    ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                    : "Bạn cần đăng nhập để thực hiện thao tác này.";
                await context.Response.WriteAsJsonAsync(new ErrorResponse(message));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Tài khoản của bạn không có quyền thực hiện thao tác này."));
            },
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Controllers: return validation errors (AC1) as ErrorResponse (AC4)
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? "Dữ liệu gửi lên không hợp lệ." // e.g. malformed JSON body
                            : e.ErrorMessage)
                        .ToArray());

            var firstMessage = errors.Values.SelectMany(m => m).FirstOrDefault()
                               ?? "Dữ liệu gửi lên không hợp lệ.";

            return new BadRequestObjectResult(new ErrorResponse(firstMessage, errors));
        };
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Apply migrations and seed roles (+ demo users in Development only)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, seedDemoUsers: app.Environment.IsDevelopment());
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // In Development the Android emulator calls http://10.0.2.2 without a trusted dev cert,
    // so HTTPS redirection is only enforced outside Development.
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
