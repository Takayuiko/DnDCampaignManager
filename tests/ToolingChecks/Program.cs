using OpenAI.Audio;
using OpenAI.Embeddings;
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

var catalogItem = new CampaignItem { CampaignId = campaign.Id, Name = "Torch", NormalizedName = "TORCH", Category = "Gear", Description = "Light", WeightLb = 1, CostGp = 0.01m };
db.CampaignItems.Add(catalogItem); await db.SaveChangesAsync();
db.CharacterItems.AddRange(new CharacterItem { CampaignId = campaign.Id, CharacterId = character.Id, ItemId = catalogItem.Id, Quantity = 3, Notes = "For the road" },
    new CharacterItem { CampaignId = campaign.Id, CharacterId = character.Id, ItemId = catalogItem.Id, Quantity = 1, Notes = "Spare" });
await db.SaveChangesAsync();
var inventory = await tools.GetCharacterInventoryAsync(scope, character.Id, default);
Check(inventory?.Items.Count == 2 && inventory.Items.Sum(i => i.Quantity) == 4 && inventory.Items[0].Notes == "For the road",
    "Inventory tool reads live assignments, quantities and notes without merging copies");
Check((await tools.GetCharacterInventoryAsync(new(owner.Id, campaign.Id), character.Id, default))?.Items.Count == 2,
    "Campaign DM can read a player's inventory");
Check(await tools.GetCharacterInventoryAsync(scope, foreignCharacter.Id, default) is null, "Inventory IDs cannot escape the campaign");
var privateCharacter = new Character { CampaignId = campaign.Id, UserId = owner.Id, Name = "DM character", Class = "Fighter", Race = "Human", Background = "Soldier", Alignment = "Neutral" };
db.Characters.Add(privateCharacter); await db.SaveChangesAsync();
Check((await tools.ExecuteAsync(scope, "GetCharacterInventory", JsonSerializer.Serialize(new { characterId = privateCharacter.Id }), default)).Contains("access denied"),
    "A member cannot use AI tools to read another player's inventory");
Check((await tools.ExecuteAsync(scope, "GetCharacterInventory", "{\"characterId\":0}", default)).Contains("error"), "Invalid inventory arguments are rejected");
using var concurrency = new AIConcurrencyLimiter(configuration);
OpenAIService AI(ScriptedOpenAI handler) => new(new ResponsesClient(new ApiKeyCredential("test-key-not-real"),
    new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) }), configuration, NullLogger<OpenAIService>.Instance, tools, concurrency);
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
using (var initial = JsonDocument.Parse(handler.Requests[0]))
    Check(initial.RootElement.GetProperty("max_output_tokens").GetInt32() == 4096 &&
        initial.RootElement.GetProperty("tools").EnumerateArray().Any(t => t.GetProperty("name").GetString() == "GetCharacterInventory"),
        "Responses request has an explicit output budget and inventory schema");
using (var next = JsonDocument.Parse(handler.Requests[1]))
    Check(next.RootElement.GetProperty("max_output_tokens").GetInt32() == 4091, "Tool continuations share the remaining output budget");
var oversized = new ScriptedOpenAI(character.Id);
try { await AI(oversized).GetChatResponseAsync([], campaignContext: new string('x', 48001)); throw new Exception("Context escaped budget"); }
catch (InvalidOperationException) { Check(oversized.Requests.Count == 0, "Oversized reference context is rejected before any provider call"); }
var boundedHistory = new ScriptedOpenAI(character.Id);
await AI(boundedHistory).GetChatResponseAsync([new AIMessage { Role = "assistant", Content = new string('x', 25000) }, new AIMessage { Role = "user", Content = "Current question" }], toolScope: scope);
Check(!boundedHistory.Requests[0].Contains(new string('x', 100)), "Oversized old history is omitted while keeping the latest question");
var streaming = new ScriptedOpenAI(character.Id);
var events = new List<AIStreamEvent>();
await foreach (var item in AI(streaming).StreamChatResponseAsync([new AIMessage { Role = "user", Content = "What is Freya's HP?" }], toolScope: scope)) events.Add(item);
Check(events.Count(x => x.Type == "completed") == 1 && events.Single(x => x.Type == "completed").Completion?.Usage.TotalTokens == 30 && string.Concat(events.Where(x => x.Type == "token").Select(x => x.Text)) == "Freya has 7 HP.", "Streaming continues after tool execution and finishes once with total usage");
var looping = new ScriptedOpenAI(character.Id) { AlwaysCalls = true };
try { await AI(looping).GetChatResponseAsync([], toolScope: scope); throw new Exception("Tool loop was unbounded"); }
catch (InvalidOperationException) { Check(looping.Requests.Count == 5, "Repeated tool calls stop at the configured round limit"); }

owner.Role = Roles.Player; await db.SaveChangesAsync();
try { await tools.GetCharacterInventoryAsync(new(owner.Id, campaign.Id), character.Id, default); throw new Exception("Roleless owner read private inventory"); }
catch (UnauthorizedAccessException) { Console.WriteLine("PASS: Inventory management access requires the current DM role"); }
owner.Role = Roles.DM; await db.SaveChangesAsync();
for (var entry = 0; entry < 101; entry++)
    db.CharacterItems.Add(new CharacterItem { CampaignId = campaign.Id, CharacterId = character.Id, ItemId = catalogItem.Id, Notes = new string('x', 2000) });
await db.SaveChangesAsync();
var boundedInventory = await tools.GetCharacterInventoryAsync(scope, character.Id, default);
Check(boundedInventory!.Items.Count <= 100 && boundedInventory.OmittedEntries == 103 - boundedInventory.Items.Count &&
    JsonSerializer.Serialize(boundedInventory.Items, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length <= 16000,
    "Inventory row and text budgets report every omitted assignment");
var inventoryCalls = new ScriptedOpenAI(character.Id) { ToolName = "GetCharacterInventory" };
await AI(inventoryCalls).GetChatResponseAsync([new AIMessage { Role = "user", Content = "What does Freya carry?" }], toolScope: scope);
Check(inventoryCalls.Requests[1].Contains("Torch") && inventoryCalls.Requests[1].Contains("quantity"), "Model inventory tool continuation receives live item data");
var budgetConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Limits:MaxRequestBytes"] = "16000" }).Build();
var byteHandler = new ScriptedOpenAI(character.Id);
var byteAI = new OpenAIService(new ResponsesClient(new ApiKeyCredential("test-key-not-real"), new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(byteHandler)) }), budgetConfig, NullLogger<OpenAIService>.Instance, tools, concurrency);
try { await byteAI.GetChatResponseAsync([new AIMessage { Role = "user", Content = new string('界', 8000) }]); throw new Exception("Serialized context escaped budget"); }
catch (InvalidOperationException) { Check(byteHandler.Requests.Count == 0, "Full request byte budget accounts for Unicode and JSON escaping before sending"); }
var smallContextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Limits:MaxContextCharacters"] = "1000" }).Build();
var boundedContext = await new CampaignKnowledgeService(db, new NoEmbeddings(), smallContextConfig, NullLogger<CampaignKnowledgeService>.Instance).BuildContextAsync(campaign.Id, player.Id, "Current characters", default);
Check(boundedContext.Prompt.Length <= 1000 && boundedContext.Prompt.Contains("CharactersOmitted"), "Reference context shrinks whole records within its configured budget");
var continuedBytes = new ScriptedOpenAI(character.Id) { ToolName = "GetCharacterInventory" };
var continuedAI = new OpenAIService(new ResponsesClient(new ApiKeyCredential("test-key-not-real"), new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(continuedBytes)) }), budgetConfig, NullLogger<OpenAIService>.Instance, tools, concurrency);
try { await continuedAI.GetChatResponseAsync([], toolScope: scope); throw new Exception("Continuation escaped byte budget"); }
catch (AIRequestLimitException) { Check(continuedBytes.Requests.Count == 1, "Large tool output is checked before sending its continuation"); }
var outputConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Limits:MaxOutputTokens"] = "256" }).Build();
var exhausted = new ScriptedOpenAI(character.Id) { AlwaysCalls = true, OutputTokens = 256 };
var outputAI = new OpenAIService(new ResponsesClient(new ApiKeyCredential("test-key-not-real"), new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(exhausted)) }), outputConfig, NullLogger<OpenAIService>.Instance, tools, concurrency);
try { await outputAI.GetChatResponseAsync([], toolScope: scope); throw new Exception("Output budget reset"); }
catch (AIRequestLimitException) { Check(exhausted.Requests.Count == 1, "Exhausted output allowance prevents another model request"); }
var missingUsage = new ScriptedOpenAI(character.Id) { MissingUsage = true };
try { await AI(missingUsage).GetChatResponseAsync([], toolScope: scope); throw new Exception("Missing usage reset the output budget"); }
catch (InvalidOperationException) { Check(missingUsage.Requests.Count == 1, "Missing provider usage cannot reset the continuation output budget"); }
var incomplete = new ScriptedOpenAI(character.Id) { Incomplete = true };
try { await AI(incomplete).GetChatResponseAsync([], toolScope: scope); throw new Exception("Incomplete response accepted"); }
catch (InvalidOperationException) { Check(incomplete.Requests.Count == 1, "Incomplete provider responses cannot become successful replies"); }
var incompleteStream = new ScriptedOpenAI(character.Id) { Incomplete = true };
var incompleteEvents = new List<AIStreamEvent>();
try { await foreach (var update in AI(incompleteStream).StreamChatResponseAsync([], toolScope: scope)) incompleteEvents.Add(update); throw new Exception("Incomplete stream accepted"); }
catch (InvalidOperationException) { Check(incompleteEvents.All(e => e.Type != "completed"), "Incomplete streaming responses never emit successful completion"); }
var deadlineConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Limits:RequestTimeoutSeconds"] = "1", ["OpenAI:Limits:EmbeddingTimeoutSeconds"] = "1", ["OpenAI:Limits:TranscriptionTimeoutSeconds"] = "1" }).Build();
var stalled = new ScriptedOpenAI(character.Id) { Pause = true };
var timedAI = new OpenAIService(new ResponsesClient(new ApiKeyCredential("test-key-not-real"), new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(stalled)) }), deadlineConfig, NullLogger<OpenAIService>.Instance, tools, concurrency);
try { await timedAI.GetChatResponseAsync([]); throw new Exception("Provider never timed out"); }
catch (OperationCanceledException) { Console.WriteLine("PASS: Provider deadline cancels a stalled request"); }

var stalledEmbeddings = new ScriptedOpenAI(character.Id) { Pause = true };
var embeddingProvider = new OpenAIEmbeddingService(new EmbeddingClient("text-embedding-3-small", new ApiKeyCredential("test-key-not-real"),
    new OpenAI.OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(stalledEmbeddings)) }), deadlineConfig, concurrency);
try { await embeddingProvider.EmbedAsync(["Test passage"], default); throw new Exception("Embedding request never timed out"); }
catch (OperationCanceledException) { Console.WriteLine("PASS: Embedding provider deadline cancels stalled ingestion/retrieval"); }
var stalledAudio = new ScriptedOpenAI(character.Id) { Pause = true };
var audioProvider = new OpenAIAudioTranscriptionService(new AudioClient("gpt-transcribe", new ApiKeyCredential("test-key-not-real"),
    new OpenAI.OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(stalledAudio)) }), deadlineConfig, concurrency);
using var fakeAudio = new MemoryStream([1, 2, 3]);
try { await audioProvider.TranscribeAsync(fakeAudio, "test.wav", default); throw new Exception("Audio request never timed out"); }
catch (OperationCanceledException) { Console.WriteLine("PASS: Transcription provider deadline cancels a stalled upload"); }

var strictConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["OpenAI:Limits:MaxConcurrentRequests"] = "1", ["OpenAI:Limits:MaxQueuedRequests"] = "0"
}).Build();
using var strictGate = new AIConcurrencyLimiter(strictConfig);
var guardedHandler = new ScriptedOpenAI(character.Id);
var guardedChat = new OpenAIService(new ResponsesClient(new ApiKeyCredential("test-key-not-real"),
    new ResponsesClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(guardedHandler)) }),
    configuration, NullLogger<OpenAIService>.Instance, tools, strictGate);
var blockedEmbeddings = new ScriptedOpenAI(character.Id);
var guardedEmbeddings = new OpenAIEmbeddingService(new EmbeddingClient("test", new ApiKeyCredential("test-key-not-real"),
    new OpenAI.OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(blockedEmbeddings)) }), configuration, strictGate);
var blockedAudio = new ScriptedOpenAI(character.Id);
var guardedAudio = new OpenAIAudioTranscriptionService(new AudioClient("test", new ApiKeyCredential("test-key-not-real"),
    new OpenAI.OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(blockedAudio)) }), configuration, strictGate);
using (var occupied = await strictGate.AcquireAsync(default))
{
    try { await guardedChat.GetChatResponseAsync([], toolScope: scope); throw new Exception("Chat escaped capacity"); }
    catch (AIBusyException) { Console.WriteLine("PASS: Chat rejects overload before a provider call"); }
    try { await foreach (var update in guardedChat.StreamChatResponseAsync([], toolScope: scope)) { } throw new Exception("Stream escaped capacity"); }
    catch (AIBusyException) { Console.WriteLine("PASS: Streaming shares the same admission limit"); }
    try { await guardedEmbeddings.EmbedAsync(["test"], default); throw new Exception("Embeddings escaped capacity"); }
    catch (AIBusyException) { Console.WriteLine("PASS: Embeddings share capacity with chat"); }
    using var audio = new MemoryStream([1, 2, 3]);
    try { await guardedAudio.TranscribeAsync(audio, "test.wav", default); throw new Exception("Audio escaped capacity"); }
    catch (AIBusyException) { Console.WriteLine("PASS: Transcription shares capacity with chat"); }
}
Check(guardedHandler.Requests.Count + blockedEmbeddings.Requests.Count + blockedAudio.Requests.Count == 0,
    "Rejected work never reaches any paid provider transport");
guardedHandler.ObserveRequest = async () => {
    try { using var permit = await strictGate.AcquireAsync(default); throw new Exception("Chat released capacity during continuation"); }
    catch (AIBusyException) { Console.WriteLine("PASS: Chat retains its permit through provider continuations"); }
};
await guardedChat.GetChatResponseAsync([], toolScope: scope);
await using (var iterator = guardedChat.StreamChatResponseAsync([], toolScope: scope).GetAsyncEnumerator())
{
    Check(await iterator.MoveNextAsync(), "Streaming emits a token while retaining its permit");
    try { using var permit = await strictGate.AcquireAsync(default); throw new Exception("Stream released active permit"); }
    catch (AIBusyException) { Console.WriteLine("PASS: An active stream blocks competing provider work"); }
}
using (var permit = await strictGate.AcquireAsync(default))
    Check(permit.IsAcquired, "Disposing a partial stream releases provider capacity");

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
    Check(discovery.IsSuccessStatusCode && body.Contains("GetCampaignState") && body.Contains("ListCharacters") && body.Contains("GetCharacter") && body.Contains("GetCharacterInventory") && !body.Contains("userId"), "MCP discovers four read-only tools without client-supplied user IDs");
}
using (var read = await Rpc("tools/call", new { name = "GetCharacter", arguments = new { campaignId = campaign.Id, characterId = character.Id } }))
    Check(read.IsSuccessStatusCode && (await read.Content.ReadAsStringAsync()).Contains("Freya"), "Authenticated MCP call reads an authorized character");
using (var inventoryRead = await Rpc("tools/call", new { name = "GetCharacterInventory", arguments = new { campaignId = campaign.Id, characterId = character.Id } }))
    Check(inventoryRead.IsSuccessStatusCode && (await inventoryRead.Content.ReadAsStringAsync()).Contains("Torch"), "MCP exposes authorized live inventory");
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
try { await tools.GetCharacterInventoryAsync(scope, character.Id, default); throw new Exception("Inventory revocation ignored"); }
catch (UnauthorizedAccessException) { Console.WriteLine("PASS: Inventory tools recheck membership after revocation"); }
Console.WriteLine("All tooling checks passed; fixture data rolls back. No paid OpenAI calls.");

sealed class NoEmbeddings : IEmbeddingService {
    public string Model => "test"; public int Dimensions => 3;
    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) => throw new Exception("No embeddings expected in tool tests");
}
sealed class ScriptedOpenAI(int characterId) : HttpMessageHandler {
    public List<string> Requests { get; } = [];
    public bool AlwaysCalls { get; init; }
    public bool MissingUsage { get; init; }
    public int OutputTokens { get; init; } = 5;
    public string ToolName { get; init; } = "GetCharacter";
    public bool Incomplete { get; init; }
    public bool Pause { get; init; }
    public Func<Task>? ObserveRequest { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        if (ObserveRequest is not null) await ObserveRequest();
        if (Pause) await Task.Delay(Timeout.Infinite, ct);
        var body = await request.Content!.ReadAsStringAsync(ct); Requests.Add(body);
        using var document = JsonDocument.Parse(body);
        var streaming = document.RootElement.GetProperty("stream").GetBoolean();
        var calls = AlwaysCalls || Requests.Count == 1;
        object[] output = calls ? [new { type = "reasoning", id = "rs_1", summary = Array.Empty<object>(), encrypted_content = "encrypted-test" }, new { type = "function_call", id = "fc_1", call_id = "call_1", name = ToolName, arguments = JsonSerializer.Serialize(new { characterId }), status = "completed" }]
            : [new { type = "message", id = "msg_1", role = "assistant", status = "completed", content = new[] { new { type = "output_text", text = "Freya has 7 HP.", annotations = Array.Empty<object>() } } }];
        var response = new { id = "resp_" + Requests.Count, @object = "response", created_at = 1, status = Incomplete ? "incomplete" : "completed", model = "gpt-5.2", output, usage = MissingUsage ? null : new { input_tokens = 10, output_tokens = OutputTokens, total_tokens = 10 + OutputTokens, input_tokens_details = new { cached_tokens = 0 }, output_tokens_details = new { reasoning_tokens = 0 } } };
        string payload;
        if (streaming) {
            payload = calls ? "" : "event: response.output_text.delta\ndata: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", sequence_number = 1, item_id = "msg_1", output_index = 0, content_index = 0, delta = "Freya has 7 HP." }) + "\n\n";
            payload += "event: response.completed\ndata: " + JsonSerializer.Serialize(new { type = "response.completed", sequence_number = 2, response }) + "\n\n";
        } else payload = JsonSerializer.Serialize(response);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, streaming ? "text/event-stream" : "application/json") };
    }
}
