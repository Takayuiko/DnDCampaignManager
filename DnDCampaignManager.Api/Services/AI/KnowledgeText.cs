namespace DnDCampaignManager.Api.Services.AI;

public static class KnowledgeText
{
    // Character-sized chunks are conservative for the embedding API's token limit.
    public static IReadOnlyList<string> Chunk(string text, int size = 1000, int overlap = 150)
    {
        if (size <= 0 || overlap < 0 || overlap >= size) throw new ArgumentOutOfRangeException(nameof(size));
        var chunks = new List<string>();
        text = text.Trim();
        for (var start = 0; start < text.Length;)
        {
            var end = Math.Min(start + size, text.Length);
            // Keep UTF-16 surrogate pairs intact at both boundaries.
            if (end < text.Length && char.IsHighSurrogate(text[end - 1])) end--;
            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);
            if (end == text.Length) break;
            var next = Math.Max(start + 1, end - overlap);
            if (char.IsLowSurrogate(text[next])) next++;
            start = next;
        }
        return chunks;
    }

    public static double Cosine(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length) return -1;
        double dot = 0, l = 0, r = 0;
        for (var i = 0; i < left.Length; i++)
        {
            if (!float.IsFinite(left[i]) || !float.IsFinite(right[i])) return -1;
            dot += (double)left[i] * right[i];
            l += (double)left[i] * left[i];
            r += (double)right[i] * right[i];
        }
        return l == 0 || r == 0 ? -1 : dot / Math.Sqrt(l * r);
    }

    public static string Limit(string? value, int length) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= length ? value : value[..length] + "…";
}
