using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IDocChunkRepository : IRepository<DocChunk>
{
    Task<List<DocChunk>> GetAllAsync();
}
