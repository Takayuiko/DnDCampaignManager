using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Responses;
using OpenAI.Embeddings;
using OpenAI.Audio;
using SkiaSharp;

void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine($"PASS: {label}"); }
var apiDirectory = Path.GetFullPath(args[0]);
var config = new ConfigurationBuilder().SetBasePath(apiDirectory).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional:true).AddUserSecrets(typeof(DnDxDbContext).Assembly, optional:true)
    .AddEnvironmentVariables().Build();
foreach (var key in new string?[] { null, "", "   ", "test-key-not-used" }) {
    var options = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OpenAI:ApiKey"] = key }).Build();
    var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(options); services.AddLogging();
    services.AddDbContext<DnDxDbContext>(b => b.UseNpgsql(config.GetConnectionString("DefaultConnection")));
    services.AddCampaignAI(options);
    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    using var scope = provider.CreateScope();
    var configured = !string.IsNullOrWhiteSpace(key);
    Check(scope.ServiceProvider.GetRequiredService<IAIService>().IsAvailable == configured &&
        scope.ServiceProvider.GetRequiredService<IEmbeddingService>().IsAvailable == configured &&
        scope.ServiceProvider.GetRequiredService<IAudioTranscriptionService>().IsAvailable == configured,
        "Missing, empty, whitespace and configured keys select the correct provider availability");
    Check(services.Any(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(MapIndexingWorker)) == configured,
        "Only configured AI registers the indexing worker");
    Check((scope.ServiceProvider.GetService<ResponsesClient>() != null) == configured &&
        (scope.ServiceProvider.GetService<EmbeddingClient>() != null) == configured &&
        (scope.ServiceProvider.GetService<AudioClient>() != null) == configured,
        "OpenAI SDK clients are created only when configured");
    _ = scope.ServiceProvider.GetRequiredService<CampaignToolService>();
    _ = scope.ServiceProvider.GetRequiredService<CampaignKnowledgeService>();
    _ = scope.ServiceProvider.GetRequiredService<MapKnowledgeService>();
}
await using var fixture = new DnDxDbContext(new DbContextOptionsBuilder<DnDxDbContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection")).Options);
var owner = new User { Email = $"optional-ai-{Guid.NewGuid():N}@example.test", Role = "DM" };
const string password = "Temporary-fixture-password-123";
owner.PasswordHash = new PasswordHasher<User>().HashPassword(owner, password);
fixture.Users.Add(owner); await fixture.SaveChangesAsync();
var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var start = new ProcessStartInfo("dotnet") { WorkingDirectory = apiDirectory, UseShellExecute = false,
    RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
start.ArgumentList.Add(Path.Combine(apiDirectory,"bin","StartupVerification","DnDCampaignManager.Api.dll"));
start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
start.ArgumentList.Add("--OpenAI:ApiKey="); start.ArgumentList.Add("--Logging:LogLevel:Default=Warning");
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["DOTNET_ENVIRONMENT"] = "Development";
using var host = new Process { StartInfo = start };
bool started = false;
using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
async Task<JsonElement> Json(HttpResponseMessage response) {
    Check(response.IsSuccessStatusCode, $"HTTP request succeeds ({(int)response.StatusCode})");
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone();
}
try {
    host.Start(); started = true;
    // Drain logs without exposing configuration or letting redirected buffers block startup.
    var stdout = host.StandardOutput.ReadToEndAsync(); var stderr = host.StandardError.ReadToEndAsync();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    while (true) {
        if (host.HasExited) throw new Exception("API exited before health check");
        try { if ((await http.GetAsync("/health",deadline.Token)).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
        await Task.Delay(100,deadline.Token);
    }
    Check(true,"Real API starts and health is available without an OpenAI key");
    var login = await Json(await http.PostAsJsonAsync("/api/auth/login",new { owner.Email, Password = password }));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",login.GetProperty("accessToken").GetString());
    _ = await Json(await http.GetAsync("/api/me"));
    var status = await Json(await http.GetAsync("/api/ai/status"));
    Check(!status.GetProperty("configured").GetBoolean(),"AI status reports unavailable while login and authenticated requests work");
    var campaign = await Json(await http.PostAsJsonAsync("/api/campaigns",new { Name="Optional AI test", Description="Temporary" }));
    var id = campaign.GetProperty("id").GetInt32();
    _ = await Json(await http.GetAsync($"/api/campaigns/{id}/characters"));
    var conversation = await Json(await http.PostAsJsonAsync("/api/ai/conversations",new { CampaignId=id }));
    var conversationId = conversation.GetProperty("id").GetGuid();
    foreach (var suffix in new[]{"messages","messages/stream"}) {
        var response = await http.PostAsJsonAsync($"/api/ai/conversations/{conversationId}/{suffix}",new { Message="Hello" });
        Check(response.StatusCode == HttpStatusCode.ServiceUnavailable &&
            (await response.Content.ReadAsStringAsync()).Contains("not configured"),"Chat and streaming return a clear 503 without a provider");
    }
    Check(!await fixture.AIMessages.AnyAsync(m => m.ConversationId == conversationId),"Unavailable chat does not persist user messages");
    var saved = await Json(await http.PostAsJsonAsync($"/api/campaigns/{id}/session-notes",
        new { SessionNumber=1,Title="Saved without AI",Content="A town by the river",PlayedOn="2026-10-06" }));
    Check(saved.GetProperty("indexStatus").GetString() == "pending","Text notes persist without a provider and keep their pending index");
    var retry = await http.PostAsync($"/api/campaigns/{id}/session-notes/{saved.GetProperty("id").GetInt64()}/index",null);
    Check(retry.StatusCode == HttpStatusCode.ServiceUnavailable,"Explicit note indexing returns 503 without changing source data");
    using var audio = new MultipartFormDataContent(); audio.Add(new ByteArrayContent([1,2,3]),"audio","session.wav");
    Check((await http.PostAsync($"/api/campaigns/{id}/session-notes/audio/transcribe",audio)).StatusCode == HttpStatusCode.ServiceUnavailable,
        "Audio transcription returns 503 instead of a dependency-resolution failure");
    using var bitmap = new SKBitmap(32,16); bitmap.Erase(SKColors.Green);
    using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png,100);
    using var mapForm = new MultipartFormDataContent();
    mapForm.Add(new StringContent("No AI map"),"title"); mapForm.Add(new StringContent("Temporary"),"description");
    mapForm.Add(new StringContent("[{\"name\":\"Town\",\"description\":\"Town by the river\",\"x\":10,\"y\":20}]"),"locationsJson");
    mapForm.Add(new ByteArrayContent(png.ToArray()),"image","map.png");
    var map = await Json(await http.PostAsync($"/api/campaigns/{id}/maps",mapForm));
    var mapId = map.GetProperty("id").GetInt64();
    Check(map.GetProperty("indexStatus").GetString() == "pending" &&
        (await http.GetAsync($"/api/campaigns/{id}/maps/{mapId}/thumbnail")).IsSuccessStatusCode,
        "Map uploads and previews work without AI");
    Check(await fixture.CampaignMaps.AnyAsync(m => m.Id == mapId && m.IndexStatus == "pending" && m.IndexLeaseId == null),
        "Unavailable indexing leaves durable map work unclaimed");
    var noKey = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["OpenAI:ApiKey"]=""}).Build();
    var services = new ServiceCollection();services.AddSingleton<IConfiguration>(noKey);services.AddLogging();
    services.AddDbContext<DnDxDbContext>(b => b.UseNpgsql(config.GetConnectionString("DefaultConnection"))); services.AddCampaignAI(noKey);
    using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
    var knowledge = scope.ServiceProvider.GetRequiredService<CampaignKnowledgeService>();
    var context = await knowledge.BuildContextAsync(id,owner.Id,"List towns",CancellationToken.None);
    Check(context.Prompt.Contains("Town by the river"),"Current campaign facts and map locations remain readable without embeddings");
    var tools = scope.ServiceProvider.GetRequiredService<CampaignToolService>();
    Check((await tools.GetCampaignStateAsync(new(owner.Id,id),CancellationToken.None)).Id == id,
        "Read-only campaign tools remain available without OpenAI");
    Check(await scope.ServiceProvider.GetRequiredService<MapKnowledgeService>().ProcessNextAsync(CancellationToken.None) == false &&
        await fixture.CampaignMaps.AnyAsync(m=>m.Id==mapId && m.IndexStatus=="pending" && m.IndexLeaseId==null),
        "Manual worker invocation cannot claim or fail queued maps while disabled");
} finally {
    if (started && !host.HasExited) { host.Kill(entireProcessTree:true); await host.WaitForExitAsync(); }
    await fixture.AIConversations.Where(c => c.UserId == owner.Id).ExecuteDeleteAsync();
    await fixture.Campaigns.Where(c => c.OwnerId == owner.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == owner.Id).ExecuteDeleteAsync();
}
Console.WriteLine("Optional AI startup checks passed; temporary fixtures removed.");
