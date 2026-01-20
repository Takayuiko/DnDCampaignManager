using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using DnDCampingManager.Api.Options;
using DnDCampingManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.EntityFrameworkCore.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers()
  .AddJsonOptions(o =>
  {
      o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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

// DbContext
var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";

builder.Services.AddDbContext<DnDxDbContext>(options =>
{
    var looksLikeSqlServer =
        cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);

    if (looksLikeSqlServer)
        options.UseSqlServer(cs);
    else
        options.UseSqlite(cs);
});

// Validate JWT secret
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
}

if (jwtOptions.SigningKeys == null || jwtOptions.SigningKeys.Count == 0)
{
    throw new InvalidOperationException("Jwt:SigningKeys must contain at least one key.");
}

if (jwtOptions.SigningKeys.Any(k => string.IsNullOrWhiteSpace(k.Kid) || string.IsNullOrWhiteSpace(k.Key)))
{
    throw new InvalidOperationException("Each Jwt:SigningKeys entry must have Kid and Key.");
}

// Service
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;

        // build dictionary of keys by kid
        var keysByKid = jwt.SigningKeys.ToDictionary(
            k => k.Kid,
            k => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(k.Key)) { KeyId = k.Kid }
        );

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,

            // Use kid from token header to pick correct signing key
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                if (!string.IsNullOrWhiteSpace(kid) && keysByKid.TryGetValue(kid, out var key))
                    return new[] { key };

                // Fallback: try all keys (useful if kid missing)
                return keysByKid.Values;
            }
        };
    });

// Authorization
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

// Build App
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DnDxDbContext>();
    var provider = db.Database.ProviderName ?? "";

    if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("FrontendPolicy");
}
else { app.UseHttpsRedirection(); }

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

