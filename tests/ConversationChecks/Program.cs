using System.Security.Claims;
using System.Text;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.DTOs.AI_DTO;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0])).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional:true).AddUserSecrets(typeof(DnDxDbContext).Assembly, optional:true)
    .AddEnvironmentVariables().Build();
DnDxDbContext Database() => new(new DbContextOptionsBuilder<DnDxDbContext>().UseNpgsql(config.GetConnectionString("DefaultConnection")).Options);
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine($"PASS: {label}"); }
await using var fixture = Database();
var owner = new User { Email = $"chat-race-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
fixture.Users.Add(owner); await fixture.SaveChangesAsync();
var campaign = new Campaign { OwnerId = owner.Id, Name = "Conversation concurrency fixture", Description = "Temporary" };
fixture.Campaigns.Add(campaign); await fixture.SaveChangesAsync();
var conversation = new AIConversation { Id = Guid.NewGuid(), UserId = owner.Id, CampaignId = campaign.Id };
var otherCampaign = new Campaign { OwnerId = owner.Id, Name = "Independent conversation fixture", Description = "Temporary" };
fixture.Campaigns.Add(otherCampaign); await fixture.SaveChangesAsync();
var other = new AIConversation { Id = Guid.NewGuid(), UserId = owner.Id, CampaignId = otherCampaign.Id };
fixture.AIConversations.AddRange(conversation, other); await fixture.SaveChangesAsync();
AIChatController Controller(DnDxDbContext db, ControlledAI ai, IConfiguration? limitsConfig = null) => new(db, ai, limitsConfig ?? config, NullLogger<AIChatController>.Instance,
    new CampaignKnowledgeService(db, new Embeddings(), config, NullLogger<CampaignKnowledgeService>.Instance)) {
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString())], "test")) } } };
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var provider = new ControlledAI { Pause = true };
Task? active = null;
try {
    foreach (var streaming in new[] { false, true }) {
        provider = new ControlledAI { Pause = true };
        await using var sending = Database();
        provider.HasTransaction = () => sending.Database.CurrentTransaction != null;
        var controller = Controller(sending, provider);
        using var output = new MemoryStream(); controller.Response.Body = output;
        active = streaming ? controller.StreamMessage(conversation.Id, new("First"), timeout.Token)
            : controller.SendMessage(conversation.Id, new("First"), timeout.Token);
        await provider.Started.Task.WaitAsync(timeout.Token);
        Check(!provider.TransactionObserved, "Provider runs without a database transaction");
        await using (var competing = Database()) {
            var unused = new ControlledAI(); var competitor = Controller(competing, unused);
            Check((await competitor.SendMessage(conversation.Id, new("Second"), timeout.Token)).Result is ConflictObjectResult,
                "Concurrent send is rejected before persisting a message");
            using var rejected = new MemoryStream(); competitor.Response.Body = rejected;
            await competitor.StreamMessage(conversation.Id, new("Third"), timeout.Token);
            Check(competitor.Response.StatusCode == 409 && Encoding.UTF8.GetString(rejected.ToArray()).Contains("active request"),
                "Concurrent stream returns a readable conflict");
            Check(await competitor.DeleteConversation(conversation.Id, timeout.Token) is ConflictObjectResult,
                "Clear cannot race with an active reply");
            Check(unused.Calls == 0, "Rejected operations never call the provider");
        }
        await using (var independent = Database())
            Check((await Controller(independent, new ControlledAI()).SendMessage(other.Id, new("Independent"), timeout.Token)).Result is OkObjectResult,
                "A different conversation can proceed independently");
        Check(await fixture.AIMessages.CountAsync(m => m.ConversationId == conversation.Id) == 1,
            "Rejected requests leave conversation history unchanged");
        provider.Release.TrySetResult(); await active;
        if (streaming) Check(Encoding.UTF8.GetString(output.ToArray()).Split("event: done").Length == 2,
            "Streaming publishes exactly one completion even if provider repeats it");
        await using (var clearing = Database())
            Check(await Controller(clearing, new ControlledAI()).DeleteConversation(conversation.Id, timeout.Token) is OkObjectResult,
                "Completed request releases its lock so clear succeeds");
        Check(!await fixture.AIMessages.AnyAsync(m => m.ConversationId == conversation.Id), "Clear removes the entire completed exchange");
    }
    foreach (var cancel in new[] { false, true }) {
        await using var failed = Database();
        var failure = new ControlledAI { Fail = !cancel, Pause = cancel };
        using var abort = new CancellationTokenSource();
        var job = Controller(failed, failure).SendMessage(conversation.Id, new("Failure"), abort.Token);
        if (cancel) { await failure.Started.Task.WaitAsync(timeout.Token); abort.Cancel(); }
        try { await job; } catch (OperationCanceledException) when (cancel) { }
        await using var recovery = Database();
        Check(await Controller(recovery, new ControlledAI()).DeleteConversation(conversation.Id, timeout.Token) is OkObjectResult,
            cancel ? "Cancellation releases the conversation lock" : "Provider failure releases the conversation lock");
    }
    var deadlineConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["OpenAI:Limits:RequestTimeoutSeconds"] = "1" }).Build();
    foreach (var stream in new[] { false, true })
    {
        await using var timedDb = Database();
        var stalled = new ControlledAI { Pause = true };
        var timedController = Controller(timedDb, stalled, deadlineConfig);
        using var timedBody = new MemoryStream();
        timedController.Response.Body = timedBody;
        if (stream)
        {
            await timedController.StreamMessage(conversation.Id, new("Timeout test"), timeout.Token);
            var body = Encoding.UTF8.GetString(timedBody.ToArray());
            Check(body.Contains("timed out") && !body.Contains("event: done"), "Stream deadline sends an error without successful completion");
        }
        else Check((await timedController.SendMessage(conversation.Id, new("Timeout test"), timeout.Token)).Result is ObjectResult { StatusCode: 504 },
            "Nonstreaming deadline returns HTTP 504");
        Check(!await timedDb.AIMessages.AnyAsync(m => m.ConversationId == conversation.Id && m.Role == "assistant"),
            "Timed-out requests do not persist a successful assistant reply");
        await using var nextDb = Database();
        Check(await Controller(nextDb, new ControlledAI()).DeleteConversation(conversation.Id, timeout.Token) is OkObjectResult,
            "Deadline releases the conversation lock for the next operation");
    }
} finally {
    provider.Release.TrySetResult();
    if (active != null) try { await active; } catch { }
    await fixture.AIConversations.Where(c => c.UserId == owner.Id).ExecuteDeleteAsync();
    await fixture.Campaigns.Where(c => (c.Id == campaign.Id || c.Id == otherCampaign.Id)).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == owner.Id).ExecuteDeleteAsync();
}
Console.WriteLine("Conversation concurrency checks passed; fixtures removed.");
sealed class ControlledAI : IAIService {
    public string Model => "test";
    public bool Pause, Fail, TransactionObserved;
    public int Calls;
    public Func<bool>? HasTransaction;
    public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<AICompletionResult> GetChatResponseAsync(IReadOnlyCollection<AIMessage> history, CancellationToken cancellationToken=default,
        string? campaignContext=null, CampaignToolScope? toolScope=null) {
        Calls++; TransactionObserved = HasTransaction?.Invoke() ?? false; Started.TrySetResult();
        if (Pause) await Release.Task.WaitAsync(cancellationToken);
        if (Fail) throw new InvalidOperationException("Test failure");
        return new("Hello", "test", null, new(1,1,2,0), 1);
    }
    public async IAsyncEnumerable<AIStreamEvent> StreamChatResponseAsync(IReadOnlyCollection<AIMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken=default,
        string? campaignContext=null, CampaignToolScope? toolScope=null) {
        var result = await GetChatResponseAsync(history,cancellationToken,campaignContext,toolScope);
        yield return new("token", "Hello");
        yield return new("completed", Completion:result);
        yield return new("completed", Completion:result);
    }
}
sealed class Embeddings : IEmbeddingService {
    public string Model => "test";
    public int Dimensions => 3;
    public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) =>
        Task.FromResult(new EmbeddingBatch(inputs.Select(_ => new float[]{1,0,0}).ToArray(),0));
}
