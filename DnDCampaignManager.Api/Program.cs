using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using DnDCampingManager.Api.Options;
using DnDCampingManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenAI.Responses;
using System.Text;

var seedDevelopmentDm = args.Contains("--seed-development-dm", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--seed-development-dm").ToArray());

if (seedDevelopmentDm)
{
    await DnDCampaignManager.Api.Services.DevelopmentDmSeeder.RunAsync(
        builder.Configuration, builder.Environment);
    return;
}

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    options.AddSecurityRequirement(new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<DnDxDbContext>(options =>
    options.UseNpgsql(connectionString));

// Validate JWT secret
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

var jwtOptions =
    builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "Jwt:Issuer and Jwt:Audience must be configured.");
}

if (jwtOptions.SigningKeys == null || jwtOptions.SigningKeys.Count == 0)
{
    throw new InvalidOperationException(
        "Jwt:SigningKeys must contain at least one key.");
}

if (jwtOptions.SigningKeys.Any(
        k => string.IsNullOrWhiteSpace(k.Kid) ||
             string.IsNullOrWhiteSpace(k.Key)))
{
    throw new InvalidOperationException(
        "Each Jwt:SigningKeys entry must have Kid and Key.");
}

// Application services
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtService, JwtService>();

// OpenAI Responses API
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    throw new InvalidOperationException(
        "OpenAI:ApiKey must be configured using .NET User Secrets or an environment variable.");
}

builder.Services.AddSingleton(new ResponsesClient(openAiApiKey));
builder.Services.AddScoped<IAIService, OpenAIService>();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

        var keysByKid = jwt.SigningKeys.ToDictionary(
            k => k.Kid,
            k => new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(k.Key))
            {
                KeyId = k.Kid
            });

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,

            IssuerSigningKeyResolver =
                (token, securityToken, kid, validationParameters) =>
                {
                    if (!string.IsNullOrWhiteSpace(kid) &&
                        keysByKid.TryGetValue(kid, out var key))
                    {
                        return new[] { key };
                    }

                    return keysByKid.Values;
                }
        };
    });

// Authorization
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("ai", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// CORS
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendPolicy", policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

// Build app
var app = builder.Build();

// Apply EF Core migrations at startup.
// Phase 1 intentionally uses migrations rather than EnsureCreated so the
// database lifecycle remains compatible with the production plan.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DnDxDbContext>();
    db.Database.Migrate();
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("FrontendPolicy");
}

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();
