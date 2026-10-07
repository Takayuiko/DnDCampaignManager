using System.Data;
using System.Security.Claims;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.DTOs.AI_DTO;
using DnDCampaignManager.Api.Models.AI;
using DnDCampaignManager.Api.Models;
using DnDCampaignManager.Api.Services.AI;
using DnDCampingManager.Api.Data;
using DnDCampingManager.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0])).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true).AddUserSecrets(typeof(DnDxDbContext).Assembly, optional: true)
    .AddEnvironmentVariables().Build();
DnDxDbContext Database() => new(new DbContextOptionsBuilder<DnDxDbContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection")).Options);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine($"PASS: {message}"); }
CampaignKnowledgeService Knowledge(DnDxDbContext db, ControlledEmbeddings provider) =>
    new(db, provider, config, NullLogger<CampaignKnowledgeService>.Instance);
CampaignSessionNotesController Controller(DnDxDbContext db, ControlledEmbeddings provider, int userId) =>
    new(db, Knowledge(db, provider)) { ControllerContext = new ControllerContext {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, Roles.DM)], "test")) } } };
void Observe(DnDxDbContext db, ControlledEmbeddings provider) => provider.CheckConnection = () => {
    Check(db.Database.CurrentTransaction is null && db.Database.GetDbConnection().State == ConnectionState.Closed,
        "Embedding call holds neither a transaction nor an open database connection");
};
await using var fixture = Database();
var originalNotes = await fixture.CampaignSessionNotes.AsNoTracking().OrderBy(n => n.Id)
    .Select(n => new { n.Id, n.Content, n.IndexStatus, n.EmbeddingModel, n.EmbeddingDimensions, n.EmbeddingInputTokens, Count = n.Chunks.Count }).ToListAsync();
await fixture.Database.MigrateAsync();
var preservedNotes = await fixture.CampaignSessionNotes.AsNoTracking().OrderBy(n => n.Id)
    .Select(n => new { n.Id, n.Content, n.IndexStatus, n.EmbeddingModel, n.EmbeddingDimensions, n.EmbeddingInputTokens, Count = n.Chunks.Count }).ToListAsync();
Check(originalNotes.SequenceEqual(preservedNotes), "Lease migration preserves existing notes, index status, metadata and chunks");
var owner = new User { Email = $"note-index-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
var replacementOwner = new User { Email = $"note-new-owner-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
try
{
    fixture.Users.AddRange(owner, replacementOwner); await fixture.SaveChangesAsync(deadline.Token);
    var campaign = new Campaign { OwnerId = owner.Id, Name = "Session indexing checks", Description = "Temporary" };
    fixture.Campaigns.Add(campaign); await fixture.SaveChangesAsync(deadline.Token);
    var request = new CreateSessionNoteRequest(1, "Tower", "The party found a silver key in the tower.", new DateOnly(2026, 10, 7));
    long noteId;
    await using (var createDb = Database())
    {
        var provider = new ControlledEmbeddings { Fail = true }; Observe(createDb, provider);
        var result = (OkObjectResult)await Controller(createDb, provider, owner.Id).Create(campaign.Id, request, deadline.Token);
        var dto = (SessionNoteDto)result.Value!; noteId = dto.Id;
        Check(dto.IndexStatus == "failed" && dto.Content == request.Content && dto.ChunkCount == 0,
            "Failed creation indexing retains readable source notes and a retryable status");
    }
    async Task Retry(ControlledEmbeddings provider)
    {
        await using var db = Database(); Observe(db, provider);
        Check(await Controller(db, provider, owner.Id).RetryIndex(campaign.Id, noteId, deadline.Token) is OkObjectResult,
            "Authorized retry returns the latest saved note");
    }
    await Retry(new ControlledEmbeddings()); await Retry(new ControlledEmbeddings());
    Check(await fixture.CampaignKnowledgeChunks.CountAsync(c => c.SessionNoteId == noteId) == 1 &&
        await fixture.CampaignSessionNotes.AsNoTracking().AnyAsync(n => n.Id == noteId && n.IndexStatus == "ready" &&
            n.IndexLeaseId == null && n.EmbeddingInputTokens == 5), "Successful retries replace chunks atomically without duplicate positions");

    await using (var indexingDb = Database())
    {
        var paused = new ControlledEmbeddings { Pause = true }; Observe(indexingDb, paused);
        var indexing = Knowledge(indexingDb, paused).IndexAsync(noteId, campaign.Id, owner.Id, deadline.Token);
        await paused.Started.Task.WaitAsync(deadline.Token);
        try
        {
            await using var competingDb = Database(); using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var competitor = new ControlledEmbeddings();
            var response = await Controller(competingDb, competitor, owner.Id).RetryIndex(campaign.Id, noteId, quick.Token);
            Check(response is OkObjectResult { Value: SessionNoteDto { IndexStatus: "pending" } } && competitor.Calls == 0,
                "Concurrent retries return promptly and do not duplicate embedding calls");
            await using (var transaction = await competingDb.Database.BeginTransactionAsync(quick.Token))
            {
                await competingDb.CampaignSessionNotes.FromSqlInterpolated($"SELECT * FROM \"CampaignSessionNotes\" WHERE \"Id\" = {noteId} FOR UPDATE").SingleAsync(quick.Token);
                await competingDb.Campaigns.FromSqlInterpolated($"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaign.Id} FOR UPDATE").SingleAsync(quick.Token);
                await transaction.RollbackAsync(quick.Token);
            }
            Check(true, "Note and campaign row locks remain available during blocked AI calls");
            var pendingContext = await Knowledge(competingDb, new ControlledEmbeddings()).BuildContextAsync(campaign.Id, owner.Id, "key", quick.Token);
            Check(!pendingContext.Sources.Any(s => s.SessionNoteId == noteId), "Pending reindexing excludes obsolete semantic chunks");
            await fixture.CampaignSessionNotes.Where(n => n.Id == noteId).ExecuteUpdateAsync(s => s.SetProperty(n => n.Content, "New source at the river."), quick.Token);
        }
        finally { paused.Release.TrySetResult(); await indexing; }
    }
    Check(await fixture.CampaignSessionNotes.AsNoTracking().AnyAsync(n => n.Id == noteId && n.Content == "New source at the river." &&
        n.IndexStatus == "pending" && n.IndexLeaseId == null), "Changed source discards a late result and releases its lease");
    await Retry(new ControlledEmbeddings());
    Check(await fixture.CampaignKnowledgeChunks.AnyAsync(c => c.SessionNoteId == noteId && c.Content == "New source at the river."),
        "Retry indexes the current source after an obsolete attempt");

    await using (var oldDb = Database())
    {
        var oldProvider = new ControlledEmbeddings { Pause = true, Marker = 9 }; Observe(oldDb, oldProvider);
        var oldJob = Knowledge(oldDb, oldProvider).IndexAsync(noteId, campaign.Id, owner.Id, deadline.Token);
        await oldProvider.Started.Task.WaitAsync(deadline.Token);
        try
        {
            await fixture.CampaignSessionNotes.Where(n => n.Id == noteId).ExecuteUpdateAsync(s => s.SetProperty(n => n.IndexLeaseUntil, DateTime.UtcNow.AddMinutes(-1)), deadline.Token);
            await Retry(new ControlledEmbeddings { Marker = 2 });
        }
        finally { oldProvider.Release.TrySetResult(); await oldJob; }
    }
    Check((await fixture.CampaignKnowledgeChunks.AsNoTracking().SingleAsync(c => c.SessionNoteId == noteId)).Embedding[0] == 2,
        "Expired leases allow recovery and late attempts cannot overwrite the new owner's chunks");

    await using (var cancelledDb = Database())
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var provider = new ControlledEmbeddings { Pause = true }; Observe(cancelledDb, provider);
        var job = Knowledge(cancelledDb, provider).IndexAsync(noteId, campaign.Id, owner.Id, cancel.Token);
        await provider.Started.Task.WaitAsync(deadline.Token); cancel.Cancel();
        var observed = false; try { await job; } catch (OperationCanceledException) { observed = true; }
        Check(observed && await fixture.CampaignSessionNotes.AsNoTracking().AnyAsync(n => n.Id == noteId &&
            n.IndexStatus == "pending" && n.IndexLeaseId == null && n.IndexLeaseUntil == null),
            "Cancellation leaves saved notes pending and immediately releases the claim for retry");
    }
    await Retry(new ControlledEmbeddings { Invalid = true });
    Check(await fixture.CampaignSessionNotes.AsNoTracking().AnyAsync(n => n.Id == noteId && n.IndexStatus == "failed" && n.IndexLeaseId == null) &&
        (await fixture.CampaignKnowledgeChunks.AsNoTracking().SingleAsync(c => c.SessionNoteId == noteId)).Embedding[0] == 2,
        "Invalid embeddings preserve the previous chunks without publishing partial results");
    await Retry(new ControlledEmbeddings());
    var context = await Knowledge(fixture, new ControlledEmbeddings()).BuildContextAsync(campaign.Id, owner.Id, "river", deadline.Token);
    Check(context.Sources.Any(s => s.SessionNoteId == noteId), "Completed indexes remain available to campaign RAG");

    await using (var longDb = Database())
    {
        var provider = new ControlledEmbeddings(); Observe(longDb, provider);
        var result = (OkObjectResult)await Controller(longDb, provider, owner.Id).Create(campaign.Id,
            request with { SessionNumber = 2, Title = "Long transcript", Content = new string('a', 35000) }, deadline.Token);
        var dto = (SessionNoteDto)result.Value!;
        Check(dto.IndexStatus == "ready" && dto.ChunkCount > 32 && provider.BatchSizes.Count > 1 &&
            provider.BatchSizes.All(size => size <= 32) && dto.EmbeddingInputTokens == dto.ChunkCount * 5,
            "Long transcripts retain bounded provider batches and total successful embedding usage");
    }
    await using (var transactionDb = Database())
    {
        await using var transaction = await transactionDb.Database.BeginTransactionAsync(deadline.Token);
        var provider = new ControlledEmbeddings(); var rejected = false;
        try { await Knowledge(transactionDb, provider).IndexAsync(noteId, campaign.Id, owner.Id, deadline.Token); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && provider.Calls == 0, "Indexing explicitly rejects callers holding an ambient EF transaction");
    }
    await using (var deleteWorkerDb = Database())
    {
        var provider = new ControlledEmbeddings { Pause = true }; Observe(deleteWorkerDb, provider);
        var job = Controller(deleteWorkerDb, provider, owner.Id).RetryIndex(campaign.Id, noteId, deadline.Token);
        await provider.Started.Task.WaitAsync(deadline.Token);
        try
        {
            await using var deleteDb = Database(); using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Check(await Controller(deleteDb, new ControlledEmbeddings(), owner.Id).Delete(campaign.Id, noteId, quick.Token) is NoContentResult,
                "Deleting a note finishes while its embedding request is blocked");
        }
        finally { provider.Release.TrySetResult(); Check(await job is NotFoundResult, "A note deleted during indexing returns 404 instead of a server error"); }
    }
    Check(!await fixture.CampaignKnowledgeChunks.AnyAsync(c => c.SessionNoteId == noteId), "Late publication cannot recreate deleted note chunks");
    var ownershipNote = new CampaignSessionNote { CampaignId = campaign.Id, SessionNumber = 3, Title = "Ownership", Content = "A changing campaign", PlayedOn = request.PlayedOn };
    fixture.CampaignSessionNotes.Add(ownershipNote); await fixture.SaveChangesAsync(deadline.Token);
    await using (var ownershipDb = Database())
    {
        var provider = new ControlledEmbeddings { Pause = true }; Observe(ownershipDb, provider);
        var job = Controller(ownershipDb, provider, owner.Id).RetryIndex(campaign.Id, ownershipNote.Id, deadline.Token);
        await provider.Started.Task.WaitAsync(deadline.Token);
        try { await fixture.Campaigns.Where(c => c.Id == campaign.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerId, replacementOwner.Id), deadline.Token); }
        finally { provider.Release.TrySetResult(); Check(await job is NotFoundResult, "Ownership changes prevent late publication and DTO disclosure"); }
    }
    Check(!await fixture.CampaignKnowledgeChunks.AnyAsync(c => c.SessionNoteId == ownershipNote.Id), "Lost campaign ownership cannot publish an index");
}
finally
{
    await fixture.Campaigns.Where(c => c.OwnerId == owner.Id || c.OwnerId == replacementOwner.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == owner.Id || u.Id == replacementOwner.Id).ExecuteDeleteAsync();
}
Console.WriteLine("All session-indexing checks passed; committed fixtures removed.");

sealed class ControlledEmbeddings : IEmbeddingService
{
    public string Model => "session-index-test";
    public int Dimensions => 2;
    public bool Pause { get; init; }
    public bool Fail { get; init; }
    public bool Invalid { get; init; }
    public float Marker { get; init; } = 1;
    public int Calls { get; private set; }
    public List<int> BatchSizes { get; } = [];
    public Action? CheckConnection { get; set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
    {
        CheckConnection?.Invoke(); Calls++; BatchSizes.Add(inputs.Count); Started.TrySetResult();
        if (Pause) await Release.Task.WaitAsync(ct);
        if (Fail) throw new InvalidOperationException("Simulated failure");
        return new EmbeddingBatch(inputs.Select(_ => Invalid ? new float[] { 0, 0 } : new float[] { Marker, 1 }).ToArray(), inputs.Count * 5);
    }
}
