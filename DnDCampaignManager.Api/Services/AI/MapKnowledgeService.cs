using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DnDCampaignManager.Api.Services.AI;

public sealed class MapKnowledgeService(DnDxDbContext db, IEmbeddingService embeddings, ILogger<MapKnowledgeService> logger)
{
    public static void QueueForIndex(CampaignMap map)
    {
        map.IndexRevision = Guid.NewGuid();
        map.IndexStatus = "pending";
        map.IndexLeaseId = null;
        map.IndexLeaseUntil = null;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!embeddings.IsAvailable) return false;
        var now = DateTime.UtcNow;
        var id = await db.CampaignMaps.AsNoTracking()
            .Where(m => m.IndexStatus == "pending" && (m.IndexLeaseUntil == null || m.IndexLeaseUntil <= now))
            .OrderBy(m => m.Id).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (id is null) return false;
        await ProcessAsync(id.Value, ct);
        return true;
    }

    public async Task ProcessAsync(long mapId, CancellationToken ct)
    {
        if (!embeddings.IsAvailable) return;
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Map embedding calls cannot run inside a database transaction.");
        var now = DateTime.UtcNow;
        // Read only source text, never map images or thumbnails.
        var source = await db.CampaignMaps.AsNoTracking()
            .Where(m => m.Id == mapId && m.IndexStatus == "pending" && (m.IndexLeaseUntil == null || m.IndexLeaseUntil <= now))
            .Select(m => new { m.Id, m.IndexRevision, m.Title, m.Description, m.LocationsJson }).SingleOrDefaultAsync(ct);
        if (source is null) return;
        var lease = Guid.NewGuid();
        var owned = db.CampaignMaps.Where(m => m.Id == mapId && m.IndexRevision == source.IndexRevision && m.IndexLeaseId == lease);
        var claimed = await db.CampaignMaps.Where(m => m.Id == mapId && m.IndexRevision == source.IndexRevision &&
            m.IndexStatus == "pending" && (m.IndexLeaseUntil == null || m.IndexLeaseUntil <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IndexLeaseId, lease).SetProperty(m => m.IndexLeaseUntil, now.AddMinutes(5)), ct);
        if (claimed == 0) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var locations = JsonSerializer.Deserialize<List<MapLocation>>(source.LocationsJson)!;
            var text = $"Map: {source.Title}\n{source.Description}\n" + string.Join("\n", locations.Select(
                x => $"Location: {x.Name}. {x.Description} (pin: {x.X:0.##}%, {x.Y:0.##}%)"));
            var passages = KnowledgeText.Chunk(text);
            var vectors = new List<float[]>();
            foreach (var group in passages.Chunk(32))
            {
                var batch = await embeddings.EmbedAsync(group, timeout.Token);
                if (batch.Vectors.Count != group.Length) throw new InvalidOperationException("Invalid embedding response.");
                vectors.AddRange(batch.Vectors);
            }
            if (vectors.Any(v => v.Length != embeddings.Dimensions || v.Any(x => !float.IsFinite(x)) || v.All(x => x == 0)))
                throw new InvalidOperationException("Invalid embedding response.");
            // Only the publish step holds a transaction. A newer revision discards this result.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var published = await owned.ExecuteUpdateAsync(s => s.SetProperty(m => m.IndexStatus, "ready")
                .SetProperty(m => m.EmbeddingModel, embeddings.Model).SetProperty(m => m.EmbeddingDimensions, embeddings.Dimensions)
                .SetProperty(m => m.IndexLeaseId, (Guid?)null).SetProperty(m => m.IndexLeaseUntil, (DateTime?)null), ct);
            if (published == 0) return;
            await db.MapKnowledgeChunks.Where(x => x.MapId == mapId).ExecuteDeleteAsync(ct);
            db.MapKnowledgeChunks.AddRange(passages.Select((content, i) => new MapKnowledgeChunk
                { MapId = mapId, Position = i, Content = content, Embedding = vectors[i] }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await owned.ExecuteUpdateAsync(s => s.SetProperty(m => m.IndexLeaseId, (Guid?)null)
                .SetProperty(m => m.IndexLeaseUntil, (DateTime?)null), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map indexing failed for {MapId}", mapId);
            await owned.ExecuteUpdateAsync(s => s.SetProperty(m => m.IndexStatus, "failed")
                .SetProperty(m => m.IndexLeaseId, (Guid?)null).SetProperty(m => m.IndexLeaseUntil, (DateTime?)null), ct);
        }
    }
}
