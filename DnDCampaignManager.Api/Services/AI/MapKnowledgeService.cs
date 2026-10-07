using DnDCampaignManager.Api.DTOs;
using DnDCampaignManager.Api.Models;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DnDCampaignManager.Api.Services.AI;

public sealed class MapKnowledgeService(DnDxDbContext db, IEmbeddingService embeddings,
    ILogger<MapKnowledgeService> logger)
{
    // Call while holding the map row lock. Source changes and index replacement commit together.
    public async Task IndexAsync(CampaignMap map, CancellationToken ct)
    {
        map.IndexStatus = "pending";
        await db.SaveChangesAsync(ct);
        try
        {
            var locations = JsonSerializer.Deserialize<List<MapLocation>>(map.LocationsJson)!;
            var text = $"Map: {map.Title}\n{map.Description}\n" + string.Join("\n", locations.Select(
                x => $"Location: {x.Name}. {x.Description} (pin: {x.X:0.##}%, {x.Y:0.##}%)"));
            var passages = KnowledgeText.Chunk(text);
            var vectors = new List<float[]>();
            foreach (var group in passages.Chunk(32))
            {
                var batch = await embeddings.EmbedAsync(group, ct);
                if (batch.Vectors.Count != group.Length) throw new InvalidOperationException("Invalid embedding response.");
                vectors.AddRange(batch.Vectors);
            }
            if (vectors.Any(v => v.Length != embeddings.Dimensions || v.Any(x => !float.IsFinite(x)) || v.All(x => x == 0)))
                throw new InvalidOperationException("Invalid embedding response.");
            db.MapKnowledgeChunks.RemoveRange(await db.MapKnowledgeChunks.Where(x => x.MapId == map.Id).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
            db.MapKnowledgeChunks.AddRange(passages.Select((text, i) => new MapKnowledgeChunk
                { MapId = map.Id, Position = i, Content = text, Embedding = vectors[i] }));
            map.EmbeddingModel = embeddings.Model;
            map.EmbeddingDimensions = embeddings.Dimensions;
            map.IndexStatus = "ready";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map indexing failed for {MapId}", map.Id);
            map.IndexStatus = "failed";
        }
        await db.SaveChangesAsync(ct);
    }
}
