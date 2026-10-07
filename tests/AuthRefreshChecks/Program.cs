using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services;
using DnDCampingManager.Api.Controllers;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using DnDCampingManager.Api.Options;
using DnDCampingManager.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DnDCampingManager.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Net;

var configuration = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0]))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true).AddEnvironmentVariables().Build();
var connection = configuration.GetConnectionString("DefaultConnection");
DnDxDbContext Database(DbCommandInterceptor? attempts = null)
{
    var options = new DbContextOptionsBuilder<DnDxDbContext>().UseNpgsql(connection);
    if (attempts is not null) options.AddInterceptors(attempts);
    return new DnDxDbContext(options.Options);
}
var jwt = new JwtService(Options.Create(new JwtOptions { Issuer = "test", Audience = "test",
    SigningKeys = [new JwtSigningKey { Kid = "test", Key = new string('x', 64) }] }));
AuthController Controller(DnDxDbContext db, int userId, string token)
{
    var http = new DefaultHttpContext();
    http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));
    http.Request.Headers.Cookie = $"refreshToken={token}";
    return new AuthController(db, new PasswordHasher<User>(), jwt) { ControllerContext = new ControllerContext { HttpContext = http } };
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}
await using var fixture = Database();
await fixture.Database.MigrateAsync();
var registrationEmail = $"registration-check-{Guid.NewGuid():N}@example.test";
var legacyEmail = $"Legacy-check-{Guid.NewGuid():N}@example.test";
var account = new User { Email = $"refresh-check-{Guid.NewGuid():N}@example.test", PasswordHash = "test-fixture", Role = Roles.Player };
fixture.Users.Add(account);
await fixture.SaveChangesAsync();
try
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
    using var provider = services.BuildServiceProvider();
    var validator = provider.GetRequiredService<IObjectModelValidator>();
    foreach (var (email, password, valid) in new (string, string, bool)[] {
        (" Person@Example.test ", new string('a', 12), true),
        ("person@example.test", new string('a', 128), true),
        ("invalid-email", new string('a', 12), false),
        (new string('a', 250) + "@example.test", new string('a', 12), false),
        ("person@example.test", "x", false),
        ("person@example.test", new string('a', 11), false),
        ("person@example.test", new string('a', 129), false),
        ("person@example.test", new string(' ', 12), false),
        ("", new string('a', 12), false) })
    {
        var action = new ActionContext(new DefaultHttpContext { RequestServices = provider },
            new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        validator.Validate(action, null, "", new RegisterRequestDto { Email = email, Password = password });
        Check(action.ModelState.IsValid == valid, $"Registration MVC validation matches expected result ({valid})");
    }
    var gate = new DuplicateCheckGate();
    await using (var registerDb1 = Database(gate))
    await using (var registerDb2 = Database(gate))
    {
        var firstRegistration = Controller(registerDb1, 0, "unused");
        var secondRegistration = Controller(registerDb2, 0, "unused");
        var results = await Task.WhenAll(
            firstRegistration.Register(new RegisterRequestDto { Email = registrationEmail.ToUpperInvariant(), Password = "a long test password" }),
            secondRegistration.Register(new RegisterRequestDto { Email = $" {registrationEmail} ", Password = "a long test password" }));
        Check(results.Count(r => r is OkObjectResult) == 1 && results.Count(r => r is BadRequestObjectResult) == 1,
            "Simultaneous case/whitespace variants register one account and return a duplicate error without a 500");
    }
    var registered = await fixture.Users.SingleAsync(u => u.NormalizedEmail == registrationEmail);
    Check(registered.Email == registrationEmail && registered.Role == Roles.Player &&
        registered.PasswordHash != "a long test password" &&
        await fixture.RefreshTokens.CountAsync(t => t.UserId == registered.Id) == 1,
        "Registration stores normalized email, a password hash, player role and one refresh token atomically");
    await using (var loginDb = Database())
    {
        var login = Controller(loginDb, 0, "unused");
        Check(await login.Login(new LoginRequestDto { Email = $" {registrationEmail.ToUpperInvariant()} ", Password = "a long test password" }) is OkObjectResult,
            "New accounts can log in with casing and surrounding whitespace handled");
        Check(await login.Login(new LoginRequestDto { Email = registrationEmail, Password = "wrong" }) is UnauthorizedResult,
            "Incorrect passwords remain unauthorized");
    }
    var legacy = new User { Email = $" {legacyEmail} ", Role = Roles.Player };
    legacy.PasswordHash = new PasswordHasher<User>().HashPassword(legacy, "old");
    fixture.Users.Add(legacy);
    await fixture.SaveChangesAsync();
    await using (var legacyDb = Database())
    {
        Check(await Controller(legacyDb, 0, "unused").Login(new LoginRequestDto {
            Email = legacyEmail.ToLowerInvariant(), Password = "old" }) is OkObjectResult,
            "Existing mixed-case accounts and older short passwords remain usable");
        Check(await Controller(legacyDb, 0, "unused").Register(new RegisterRequestDto {
            Email = legacyEmail.ToLowerInvariant(), Password = "a long test password" }) is BadRequestObjectResult,
            "Registration cannot duplicate a legacy account by normalizing its email");
    }
    // Separate connections need committed fixtures; finally removes this test account and its tokens.
    var original = RefreshTokenService.Create(account.Id);
    fixture.RefreshTokens.Add(original);
    await fixture.SaveChangesAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    async Task<(IActionResult First, IActionResult Second, AuthController SecondController)> Overlap(string token, bool logout)
    {
        var attempts = new LockAttempts();
        await using var blocker = Database();
        await using var held = await blocker.Database.BeginTransactionAsync(timeout.Token);
        await blocker.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {account.Id} FOR UPDATE")
            .SingleAsync(timeout.Token);
        await using var firstDb = Database(attempts);
        await using var secondDb = Database(attempts);
        var first = Controller(firstDb, account.Id, token);
        var second = Controller(secondDb, account.Id, token);
        var firstTask = first.Refresh(timeout.Token);
        var secondTask = logout ? second.Logout(timeout.Token) : second.Refresh(timeout.Token);
        try
        {
            await attempts.BothStarted.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            await held.RollbackAsync(CancellationToken.None);
        }
        var results = await Task.WhenAll(firstTask, secondTask);
        return (results[0], results[1], second);
    }

    var rotated = await Overlap(original.Token, logout: false);
    Check(new[] { rotated.First, rotated.Second }.Count(x => x is OkObjectResult) == 1 &&
        new[] { rotated.First, rotated.Second }.Count(x => x is UnauthorizedResult) == 1,
        "Overlapping refreshes produce exactly one success and one unauthorized response");
    fixture.ChangeTracker.Clear();
    var tokens = await fixture.RefreshTokens.Where(t => t.UserId == account.Id).ToListAsync();
    var replacement = tokens.Single(t => !t.IsRevoked);
    Check(tokens.Count == 2 && tokens.Single(t => t.Id == original.Id).ReplacedByToken == replacement.Token,
        "A used token is revoked and has exactly one persisted replacement");
    await using (var replayDb = Database())
    {
        var replay = Controller(replayDb, account.Id, original.Token);
        Check(await replay.Refresh(timeout.Token) is UnauthorizedResult && !replay.Response.Headers.ContainsKey("Set-Cookie"),
            "Replaying a consumed token fails without issuing a cookie");
    }
    await using (var nextDb = Database())
    {
        var next = Controller(nextDb, account.Id, replacement.Token);
        Check(await next.Refresh(timeout.Token) is OkObjectResult &&
            next.Response.Headers.SetCookie.ToString().Contains("path=/api/auth") &&
            next.Response.Headers.SetCookie.ToString().Contains("httponly") &&
            next.Response.Headers.SetCookie.ToString().Contains("secure"),
            "The replacement remains usable and keeps the protected cookie settings");
    }
    fixture.ChangeTracker.Clear();
    replacement = await fixture.RefreshTokens.SingleAsync(t => t.UserId == account.Id && !t.IsRevoked);
    var versionBefore = await fixture.Users.Where(u => u.Id == account.Id).Select(u => u.TokenVersion).SingleAsync();
    var signedOut = await Overlap(replacement.Token, logout: true);
    Check(signedOut.Second is OkResult, "Logout succeeds while refresh is competing");
    Check(!await fixture.RefreshTokens.AnyAsync(t => t.UserId == account.Id && !t.IsRevoked) &&
        await fixture.Users.Where(u => u.Id == account.Id).Select(u => u.TokenVersion).SingleAsync() == versionBefore + 1,
        "Concurrent logout leaves no active refresh tokens and increments the version once");
    Check(signedOut.SecondController.Response.Headers.SetCookie.ToString().Contains("path=/api/auth") &&
        signedOut.SecondController.Response.Headers.SetCookie.ToString().Contains("expires="),
        "Logout expires the refresh cookie at its original path");
    if (signedOut.First is OkObjectResult issued)
    {
        var accessToken = JsonSerializer.SerializeToElement(issued.Value).GetProperty("accessToken").GetString()!;
        var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Role, Roles.Player),
            new Claim("token_version", parsed.Claims.Single(c => c.Type == "token_version").Value)], "test"));
        Check(!await new CurrentTokenValidator(fixture).IsCurrentAsync(principal, timeout.Token),
            "An access token issued before competing logout is invalidated");
    }
    else Check(signedOut.First is UnauthorizedResult, "Refresh after competing logout is unauthorized");
    await using var revokedDb = Database();
    Check(await Controller(revokedDb, account.Id, replacement.Token).Refresh(timeout.Token) is UnauthorizedResult,
        "A logged-out refresh token cannot recreate the session");
    var expired = RefreshTokenService.Create(account.Id);
    expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
    fixture.RefreshTokens.Add(expired);
    await fixture.SaveChangesAsync();
    await using var expiredDb = Database();
    Check(await Controller(expiredDb, account.Id, expired.Token).Refresh(timeout.Token) is UnauthorizedResult &&
        await Controller(expiredDb, account.Id, "unknown-token").Refresh(timeout.Token) is UnauthorizedResult,
        "Expired and unknown refresh tokens remain unauthorized");
}
finally
{
    var fixtureIds = fixture.Users.Where(u => u.Id == account.Id || u.NormalizedEmail == registrationEmail ||
        u.NormalizedEmail == legacyEmail.ToLowerInvariant()).Select(u => u.Id);
    await fixture.RefreshTokens.Where(t => fixtureIds.Contains(t.UserId)).ExecuteDeleteAsync();
    await fixture.Users.Where(u => fixtureIds.Contains(u.Id)).ExecuteDeleteAsync();
}
var webBuilder = WebApplication.CreateBuilder();
webBuilder.Logging.ClearProviders();
webBuilder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    AuthenticationRateLimits.Configure(options);
});
await using (var web = webBuilder.Build())
{
    web.Urls.Add("http://127.0.0.1:0");
    web.UseRateLimiter();
    foreach (var method in new[] { "Register", "Login", "Refresh" })
        web.MapPost($"/{method}", () => Results.Ok())
            .WithMetadata(typeof(AuthController).GetMethod(method)!.GetCustomAttribute<EnableRateLimitingAttribute>()!);
    await web.StartAsync();
    try
    {
        using var http = new HttpClient { BaseAddress = new Uri(web.Urls.Single()) };
        for (var i = 0; i < 10; i++)
        {
            using var response = await http.PostAsync(i % 2 == 0 ? "/Register" : "/Login", null);
            Check(response.StatusCode == HttpStatusCode.OK, "Authentication permits requests within its shared IP limit");
        }
        using var limited = await http.PostAsync("/Login", null);
        Check(limited.StatusCode == HttpStatusCode.TooManyRequests, "Login and registration share the 10-per-minute IP limit");
        for (var i = 0; i < 60; i++)
        {
            using var response = await http.PostAsync("/Refresh", null);
            if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Refresh incorrectly shares the login limit.");
        }
        using var refreshLimited = await http.PostAsync("/Refresh", null);
        Check(refreshLimited.StatusCode == HttpStatusCode.TooManyRequests, "Refresh has an independent 60-per-minute IP limit");
    }
    finally { await web.StopAsync(); }
}
Console.WriteLine("All authentication checks passed; committed test fixtures removed.");

sealed class LockAttempts : DbCommandInterceptor
{
    private int count;
    public TaskCompletionSource BothStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FOR UPDATE") && Interlocked.Increment(ref count) == 2)
            BothStarted.TrySetResult();
        return ValueTask.FromResult(result);
    }
}

sealed class DuplicateCheckGate : DbCommandInterceptor
{
    private int count;
    private readonly TaskCompletionSource bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("SELECT EXISTS") && command.CommandText.Contains("NormalizedEmail"))
        {
            if (Interlocked.Increment(ref count) == 2) bothRead.TrySetResult();
            await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        return result;
    }
}
