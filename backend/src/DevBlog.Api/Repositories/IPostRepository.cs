using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IPostRepository : IRepository<Post>
{
    Task<PagedResult<PostListItem>> GetPagedAsync(int page, int pageSize, string? tag);
}
