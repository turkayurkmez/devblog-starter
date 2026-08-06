using DevBlog.Api.Data;
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class DocChunkRepository(AppDbContext db) : Repository<DocChunk>(db), IDocChunkRepository
{
    public Task<List<DocChunk>> GetAllAsync() =>
        Db.DocChunks.AsNoTracking().ToListAsync();
}
