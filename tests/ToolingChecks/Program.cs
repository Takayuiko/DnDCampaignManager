using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Services.AI;
using DnDCampaignManager.Api.Services;

#pragma warning disable OPENAI001
var configuration = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0]))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true).AddEnvironmentVariables().Build();
await using var db = new DnDxDbContext(new DbContextOptionsBuilder<DnDxDbContext>()
    .UseNpgsql(configuration.GetConnectionString("DefaultConnection")).Options);
await using var transaction = await db.Database.BeginTransactionAsync();
var suffix = Guid.NewGuid().ToString("N");
var owner = new User { Email = $"tools-owner-{suffix}@example.test", Role = Roles.DM, PasswordHash = "unused" };
var player = new User { Email = $"tools-player-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused" };
var outsider = new User { Email = $"tools-outsider-{suffix}@example.test", Role = Roles.Player, PasswordHash = "unused" };
db.Users.AddRange(owner, player, outsider); await db.SaveChangesAsync();
var campaign = new Campaign { Name = "Tool campaign", Description = "Shared facts", OwnerId = owner.Id };
var foreign = new Campaign { Name = "Foreign secret", Description = "Must not leak", OwnerId = outsider.Id };
db.Campaigns.AddRange(campaign, foreign); await db.SaveChangesAsync();
db.CampaignPlayer.Add(new CampaignPlayer { CampaignId = campaign.Id, UserId = player.Id });
var character = new Character { CampaignId = campaign.Id, UserId = player.Id, Name = "Freya", Class = "Fighter", Race = "Human", Background = "Soldier", Alignment = "Neutral", HitPointCurrent = 7, HitPointMax = 10 };
var foreignCharacter = new Character { CampaignId = foreign.Id, UserId = outsider.Id, Name = "Hidden", Class = "Wizard", Race = "Human", Background = "Sage", Alignment = "Neutral" };
db.Characters.AddRange(character, foreignCharacter); await db.SaveChangesAsync();
var knowledge = new CampaignKnowledgeService(db, new NoEmbeddings(), configuration, NullLogger<CampaignKnowledgeService>.Instance);
var tools = new CampaignToolService(db, knowledge);
var scope = new CampaignToolScope(player.Id, campaign.Id);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
Check((await tools.GetCampaignStateAsync(scope, default)).Name == "Tool campaign", "Member reads campaign facts");
Check((await tools.ListCharactersAsync(scope, default)).Single().Id == character.Id, "Character list is scoped to the campaign");
Check((await tools.GetCharacterAsync(scope, character.Id, default))?.HitPointCurrent == 7, "Character tool returns current HP");
Check(await tools.GetCharacterAsync(scope, foreignCharacter.Id, default) is null, "Cross-campaign character ID returns no data");
foreach (var (name, arguments) in new[] { ("DeleteCampaign", "{}"), ("GetCharacter", "{bad"), ("GetCharacter", "{\"characterId\":\"oops\"}"), ("GetCampaignState", "{\"campaignId\":1}"), ("GetCharacter", "{\"characterId\":-1}") })
    Check((await tools.ExecuteAsync(scope, name, arguments, default)).Contains("error"), "Invalid/unknown model tool arguments are rejected");
try { await tools.GetCampaignStateAsync(new(outsider.Id, campaign.Id), default); throw new Exception("Access escaped"); }
catch (UnauthorizedAccessException) { Console.WriteLine("PASS: Nonmember tool access is denied"); }
Check(!(await tools.ExecuteAsync(scope, "GetCampaignState", "{}", default)).Contains("Email"), "Tool output excludes account data");

OpenAIService AI(ScriptedOpenAI handler) => new(new ResponsesClient(new ApiKeyCredential("test-key-not-real"),
    new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) }), configuration, NullLogger<OpenAIService>.Instance, tools);
var handler = new ScriptedOpenAI(character.Id);
var completion = await AI(handler).GetChatResponseAsync([new AIMessage { Role = "user", Content = "What is Freya's HP?" }], toolScope: scope);
Check(completion.Reply == "Freya has 7 HP." && completion.Usage.TotalTokens == 30 && completion.Usage.InputTokens == 20 && completion.Usage.OutputTokens == 10,
    "Nonstreaming tool loop aggregates usage across both model requests");
using (var followup = JsonDocument.Parse(handler.Requests[1]))
{
    var inputs = followup.RootElement.GetProperty("input").EnumerateArray().ToArray();
    Check(inputs.Any(x => x.GetProperty("type").GetString() == "function_call_output" && x.GetProperty("call_id").GetString() == "call_1" && x.GetProperty("output").GetString()!.Contains("hitPointCurrent")), "Correct call ID and application result are returned to the model");
    Check(inputs.Any(x => x.GetProperty("type").GetString() == "reasoning"), "Reasoning items survive the tool continuation");
    Check(followup.RootElement.GetProperty("store").GetBoolean() == false && followup.RootElement.GetProperty("include").EnumerateArray().Any(x => x.GetString() == "reasoning.encrypted_content"), "Stateless Responses continuation includes encrypted reasoning");
}
var streaming = new ScriptedOpenAI(character.Id);
var events = new List<AIStreamEvent>();
await foreach (var item in AI(streaming).StreamChatResponseAsync([new AIMessage { Role = "user", Content = "What is Freya's HP?" }], toolScope: scope)) events.Add(item);
Check(events.Count(x => x.Type == "completed") == 1 && events.Single(x => x.Type == "completed").Completion?.Usage.TotalTokens == 30 && string.Concat(events.Where(x => x.Type == "token").Select(x => x.Text)) == "Freya has 7 HP.", "Streaming continues after tool execution and finishes once with total usage");
var looping = new ScriptedOpenAI(character.Id) { AlwaysCalls = true };
try { await AI(looping).GetChatResponseAsync([], toolScope: scope); throw new Exception("Tool loop was unbounded"); }
catch (InvalidOperationException) { Check(looping.Requests.Count == 5, "Repeated tool calls stop at the configured round limit"); }

// Exercise the real MCP HTTP transport with JWT authentication, using rolled-back database fixtures.
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddSingleton(tools);
builder.Services.AddSingleton(db);
builder.Services.AddScoped<CurrentTokenValidator>();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('t', 64)));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    o.TokenValidationParameters = new TokenValidationParameters
        { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = key };
    o.Events = new JwtBearerEvents { OnTokenValidated = async context => {
        if (!await context.HttpContext.RequestServices.GetRequiredService<CurrentTokenValidator>()
            .IsCurrentAsync(context.Principal, context.HttpContext.RequestAborted)) context.Fail("Stale account permissions");
    } };
});
builder.Services.AddAuthorization();
builder.Services.AddMcpServer().WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless).WithTools<CampaignMcpTools>();
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
async Task<HttpResponseMessage> Rpc(string method, object parameters) {
    var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params = parameters }), Encoding.UTF8, "application/json") };
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
    return await client.SendAsync(request);
}
using (var anonymous = await Rpc("tools/list", new { })) Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "MCP rejects unauthenticated HTTP requests");
string Token(User user) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Role, user.Role), new Claim("token_version", user.TokenVersion.ToString())],
    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(player));
using (var initialize = await Rpc("initialize", new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "ToolingChecks", version = "1.0" } }))
    Check(initialize.IsSuccessStatusCode && (await initialize.Content.ReadAsStringAsync()).Contains("serverInfo"), "MCP initializes with a standard protocol client");
using (var discovery = await Rpc("tools/list", new { })) {
    var body = await discovery.Content.ReadAsStringAsync();
    Check(discovery.IsSuccessStatusCode && body.Contains("GetCampaignState") && body.Contains("ListCharacters") && body.Contains("GetCharacter") && !body.Contains("userId"), "MCP discovers three read-only tools without client-supplied user IDs");
}
using (var read = await Rpc("tools/call", new { name = "GetCharacter", arguments = new { campaignId = campaign.Id, characterId = character.Id } }))
    Check(read.IsSuccessStatusCode && (await read.Content.ReadAsStringAsync()).Contains("Freya"), "Authenticated MCP call reads an authorized character");
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(outsider));
using (var denied = await Rpc("tools/call", new { name = "GetCampaignState", arguments = new { campaignId = campaign.Id } })) {
    var body = await denied.Content.ReadAsStringAsync();
    Check(body.Contains("access denied") && !body.Contains("Shared facts"), "MCP rejects another user's campaign without leaking data");
}
outsider.TokenVersion++; await db.SaveChangesAsync();
using (var stale = await Rpc("tools/list", new { })) Check(stale.StatusCode == HttpStatusCode.Unauthorized,
    "MCP rejects stale tokens through the existing current-token validator");
await app.StopAsync();
db.CampaignPlayer.Remove(await db.CampaignPlayer.SingleAsync(x => x.CampaignId == campaign.Id && x.UserId == player.Id)); await db.SaveChangesAsync();
try { await tools.GetCharacterAsync(scope, character.Id, default); throw new Exception("Revocation ignored"); }
catch (UnauthorizedAccessException) { Console.WriteLine("PASS: Tools recheck membership after revocation"); }
Console.WriteLine("All tooling checks passed; fixture data rolls back. No paid OpenAI calls.");

sealed class NoEmbeddings : IEmbeddingService {
    public string Model => "test"; public int Dimensions => 3;
    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) => throw new Exception("No embeddings expected in tool tests");
}
sealed class ScriptedOpenAI(int characterId) : HttpMessageHandler {
    public List<string> Requests { get; } = [];
    public bool AlwaysCalls { get; init; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        var body = await request.Content!.ReadAsStringAsync(ct); Requests.Add(body);
        using var document = JsonDocument.Parse(body);
        var streaming = document.RootElement.GetProperty("stream").GetBoolean();
        var calls = AlwaysCalls || Requests.Count == 1;
        object[] output = calls ? [new { type = "reasoning", id = "rs_1", summary = Array.Empty<object>(), encrypted_content = "encrypted-test" }, new { type = "function_call", id = "fc_1", call_id = "call_1", name = "GetCharacter", arguments = JsonSerializer.Serialize(new { characterId }), status = "completed" }]
            : [new { type = "message", id = "msg_1", role = "assistant", status = "completed", content = new[] { new { type = "output_text", text = "Freya has 7 HP.", annotations = Array.Empty<object>() } } }];
        var response = new { id = "resp_" + Requests.Count, @object = "response", created_at = 1, status = "completed", model = "gpt-5.2", output, usage = new { input_tokens = 10, output_tokens = 5, total_tokens = 15, input_tokens_details = new { cached_tokens = 0 }, output_tokens_details = new { reasoning_tokens = 0 } } };
        string payload;
        if (streaming) {
            payload = calls ? "" : "event: response.output_text.delta\ndata: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", sequence_number = 1, item_id = "msg_1", output_index = 0, content_index = 0, delta = "Freya has 7 HP." }) + "\n\n";
            payload += "event: response.completed\ndata: " + JsonSerializer.Serialize(new { type = "response.completed", sequence_number = 2, response }) + "\n\n";
        } else payload = JsonSerializer.Serialize(response);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, streaming ? "text/event-stream" : "application/json") };
    }
}
