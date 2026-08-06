using System.Text.Json;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public class DocChunkService(IDocChunkRepository docChunkRepository) : IDocChunkService
{
    public async Task<IReadOnlyList<DocChunkSearchResult>> SearchAsync(float[] queryVector, int topK = 5)
    {
        var chunks = await docChunkRepository.GetAllAsync();

        return chunks
            .Select(chunk => new DocChunkSearchResult(
                chunk.Id,
                chunk.SourceFile,
                chunk.ChunkIndex,
                chunk.Content,
                CosineSimilarity(queryVector, JsonSerializer.Deserialize<float[]>(chunk.VectorJson)!)))
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Vektör boyutları eşleşmiyor ({a.Length} vs {b.Length}).");
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
