using DevBlog.Api.Data;
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class PostRepository(AppDbContext db) : Repository<Post>(db), IPostRepository
{
    public async Task<PagedResult<PostListItem>> GetPagedAsync(int page, int pageSize, string? tag)
    {
        var query = Db.Posts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var escapedTag = tag.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(p =>
                EF.Functions.Like(p.Tags, escapedTag, "\\") ||
                EF.Functions.Like(p.Tags, escapedTag + ",%", "\\") ||
                EF.Functions.Like(p.Tags, "%," + escapedTag, "\\") ||
                EF.Functions.Like(p.Tags, "%," + escapedTag + ",%", "\\"));
        }

        query = query.OrderByDescending(p => p.PublishedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostListItem(p.Id, p.Title, p.Slug, p.Tags, p.PublishedAt, p.Author.Username))
            .ToListAsync();

        return new PagedResult<PostListItem>(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
