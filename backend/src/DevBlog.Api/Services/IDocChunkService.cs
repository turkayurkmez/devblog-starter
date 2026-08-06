using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public interface IDocChunkService
{
    Task<IReadOnlyList<DocChunkSearchResult>> SearchAsync(float[] queryVector, int topK = 5);
}
