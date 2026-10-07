using System.Security.Claims;
using System.Text.Json;
using DnDCampaignManager.Api.Controllers;
using DnDCampaignManager.Api.DTOs;
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
await using var fixture = Database();
await fixture.Database.MigrateAsync();
var owner = new User { Email = $"map-worker-{Guid.NewGuid():N}@example.test", Role = Roles.DM, PasswordHash = "fixture" };
fixture.Users.Add(owner);
await fixture.SaveChangesAsync();
var campaign = new Campaign { OwnerId = owner.Id, Name = "Map indexing test", Description = "Temporary" };
fixture.Campaigns.Add(campaign);
await fixture.SaveChangesAsync();
var map = new CampaignMap { CampaignId = campaign.Id, Title = "Worker map", NormalizedTitle = "WORKER MAP", Description = "Old source",
    Image = [1, 2, 3], ImageContentType = "image/png", LocationsJson = JsonSerializer.Serialize(new[] { new MapLocation("Town", "Old source", 10, 20) }) };
fixture.CampaignMaps.Add(map);
await fixture.SaveChangesAsync();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
MapKnowledgeService Indexer(DnDxDbContext db, ControlledEmbeddings embeddings) => new(db, embeddings, NullLogger<MapKnowledgeService>.Instance);
CampaignKnowledgeService Knowledge(DnDxDbContext db, ControlledEmbeddings embeddings) =>
    new(db, embeddings, config, NullLogger<CampaignKnowledgeService>.Instance);
CampaignMapsController Controller(DnDxDbContext db, ControlledEmbeddings embeddings)
{
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()), new Claim(ClaimTypes.Role, Roles.DM)], "test")) };
    return new CampaignMapsController(db, Knowledge(db, embeddings)) { ControllerContext = new ControllerContext { HttpContext = http } };
}
var blocked = new ControlledEmbeddings { Pause = true };
Task? blockedJob = null;
try
{
    await using var workerDb = Database();
    blocked.HasTransaction = () => workerDb.Database.CurrentTransaction is not null;
    blockedJob = Indexer(workerDb, blocked).ProcessAsync(map.Id, timeout.Token);
    await blocked.Started.Task.WaitAsync(timeout.Token);
    Check(!blocked.TransactionObserved, "Embedding calls run without an open database transaction");
    var activeRevision = await fixture.CampaignMaps.AsNoTracking().Where(m => m.Id == map.Id).Select(m => m.IndexRevision).SingleAsync();
    await using (var pendingRetryDb = Database())
        await Controller(pendingRetryDb, new ControlledEmbeddings()).Retry(campaign.Id, map.Id, timeout.Token);
    Check(await fixture.CampaignMaps.AsNoTracking().AnyAsync(m => m.Id == map.Id && m.IndexRevision == activeRevision && m.IndexLeaseId != null),
        "Repeated retries do not invalidate or duplicate an already pending job");
    await using (var competitorDb = Database())
    {
        var competitor = new ControlledEmbeddings();
        await Indexer(competitorDb, competitor).ProcessAsync(map.Id, timeout.Token);
        Check(competitor.Calls == 0, "An active lease prevents a second worker indexing the same revision");
    }
    await using (var editDb = Database())
    {
        using var lockTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await using (var transaction = await editDb.Database.BeginTransactionAsync(lockTimeout.Token))
        {
            await editDb.Campaigns.FromSqlInterpolated($"SELECT * FROM \"Campaigns\" WHERE \"Id\" = {campaign.Id} FOR UPDATE")
                .SingleAsync(lockTimeout.Token);
            await transaction.RollbackAsync(lockTimeout.Token);
        }
        var unusedProvider = new ControlledEmbeddings();
        var updated = (OkObjectResult)await Controller(editDb, unusedProvider).Update(campaign.Id, map.Id,
            new SaveMapRequest { Title = map.Title, Description = "New source",
                LocationsJson = JsonSerializer.Serialize(new[] { new MapLocation("Town", "New source", 10, 20) }) }, lockTimeout.Token);
        Check(((MapDto)updated.Value!).IndexStatus == "pending" && unusedProvider.Calls == 0,
            "Campaign locks and map edits remain available while the old embedding call is blocked");
    }
    await using (var newWorkerDb = Database())
        await Indexer(newWorkerDb, new ControlledEmbeddings()).ProcessAsync(map.Id, timeout.Token);
    blocked.Release.TrySetResult();
    await blockedJob;
    Check(await fixture.CampaignMaps.AsNoTracking().AnyAsync(m => m.Id == map.Id && m.IndexStatus == "ready") &&
        await fixture.MapKnowledgeChunks.AnyAsync(c => c.MapId == map.Id && c.Content.Contains("New source")) &&
        !await fixture.MapKnowledgeChunks.AnyAsync(c => c.MapId == map.Id && c.Content.Contains("Old source")),
        "A late result from an older revision cannot overwrite the new index");
    var ready = await Knowledge(fixture, new ControlledEmbeddings()).BuildContextAsync(campaign.Id, owner.Id, "Town", timeout.Token);
    Check(ready.Sources.Any(s => s.MapId == map.Id), "Completed background indexes retain map references in RAG retrieval");
    await using (var retryDb = Database())
    {
        var unused = new ControlledEmbeddings();
        var response = (OkObjectResult)await Controller(retryDb, unused).Retry(campaign.Id, map.Id, timeout.Token);
        Check(((MapDto)response.Value!).IndexStatus == "pending" && unused.Calls == 0, "Retry queues durable work without calling AI");
    }
    await using (var failedDb = Database())
        await Indexer(failedDb, new ControlledEmbeddings { Fail = true }).ProcessAsync(map.Id, timeout.Token);
    Check(await fixture.CampaignMaps.AsNoTracking().AnyAsync(m => m.Id == map.Id && m.IndexStatus == "failed" && m.Description == "New source" && m.Image.Length == 3),
        "Provider failure preserves the map and records a retryable failed status");
    var failed = await Knowledge(fixture, new ControlledEmbeddings()).BuildContextAsync(campaign.Id, owner.Id, "Town", timeout.Token);
    Check(!failed.Sources.Any(s => s.MapId == map.Id), "Failed or pending maps cannot expose obsolete semantic chunks");
    await using (var retryDb = Database()) await Controller(retryDb, new ControlledEmbeddings()).Retry(campaign.Id, map.Id, timeout.Token);
    await using (var successDb = Database()) await Indexer(successDb, new ControlledEmbeddings()).ProcessAsync(map.Id, timeout.Token);
    Check(await fixture.MapKnowledgeChunks.CountAsync(c => c.MapId == map.Id) == 1,
        "Retry replaces chunks without duplicate positions");
    await fixture.CampaignMaps.Where(m => m.Id == map.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.IndexStatus, "pending")
        .SetProperty(m => m.IndexLeaseId, Guid.NewGuid()).SetProperty(m => m.IndexLeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
    await using (var recoveryDb = Database()) await Indexer(recoveryDb, new ControlledEmbeddings()).ProcessAsync(map.Id, timeout.Token);
    Check(await fixture.CampaignMaps.AsNoTracking().AnyAsync(m => m.Id == map.Id && m.IndexStatus == "ready" && m.IndexLeaseId == null),
        "Expired leases recover interrupted jobs in a new worker context");

    await using (var cancellationDb = Database())
    {
        await Controller(cancellationDb, new ControlledEmbeddings()).Retry(campaign.Id, map.Id, timeout.Token);
        var paused = new ControlledEmbeddings { Pause = true };
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var cancelledJob = Indexer(cancellationDb, paused).ProcessAsync(map.Id, shutdown.Token);
        await paused.Started.Task.WaitAsync(timeout.Token);
        shutdown.Cancel();
        var cancelled = false;
        try { await cancelledJob; } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && await fixture.CampaignMaps.AsNoTracking().AnyAsync(m => m.Id == map.Id &&
            m.IndexStatus == "pending" && m.IndexLeaseId == null && m.IndexLeaseUntil == null),
            "Worker shutdown releases its lease and leaves durable pending work");
    }

    await using var deletionDb = Database();
    await Controller(deletionDb, new ControlledEmbeddings()).Retry(campaign.Id, map.Id, timeout.Token);
    var deletionPause = new ControlledEmbeddings { Pause = true };
    await using var deletionWorkerDb = Database();
    var deletionJob = Indexer(deletionWorkerDb, deletionPause).ProcessAsync(map.Id, timeout.Token);
    await deletionPause.Started.Task.WaitAsync(timeout.Token);
    try { Check(await Controller(deletionDb, new ControlledEmbeddings()).Delete(campaign.Id, map.Id, timeout.Token) is NoContentResult,
        "Maps can be deleted while embedding calls are in flight"); }
    finally { deletionPause.Release.TrySetResult(); await deletionJob; }
    Check(!await fixture.MapKnowledgeChunks.AnyAsync(c => c.MapId == map.Id), "Late results cannot recreate a deleted map's index");
}
finally
{
    blocked.Release.TrySetResult();
    if (blockedJob is not null) { try { await blockedJob; } catch (OperationCanceledException) { } }
    await fixture.Campaigns.Where(c => c.Id == campaign.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == owner.Id).ExecuteDeleteAsync();
}
Console.WriteLine("All background map-indexing checks passed; committed fixtures removed.");

sealed class ControlledEmbeddings : IEmbeddingService
{
    public string Model => "map-index-test";
    public int Dimensions => 2;
    public bool Pause { get; init; }
    public bool Fail { get; init; }
    public int Calls { get; private set; }
    public Func<bool>? HasTransaction { get; set; }
    public bool TransactionObserved { get; private set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
    {
        Calls++;
        TransactionObserved |= HasTransaction?.Invoke() == true;
        Started.TrySetResult();
        if (Pause) await Release.Task.WaitAsync(ct);
        if (Fail) throw new InvalidOperationException("Simulated embedding failure");
        return new EmbeddingBatch(inputs.Select(_ => new float[] { 1, 0 }).ToArray(), 10);
    }
}
