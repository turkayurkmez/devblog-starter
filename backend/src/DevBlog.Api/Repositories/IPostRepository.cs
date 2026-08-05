using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IPostRepository : IRepository<Post>
{
    Task<PagedResult<PostListItem>> GetPagedAsync(int page, int pageSize, string? tag, int? currentUserId);

    /// <summary>Lightweight lookup used to resolve a post's identity (e.g. for liking) without pulling comments/likes.</summary>
    Task<Post?> GetBySlugAsync(string slug);

    Task<PostDetail?> GetDetailBySlugAsync(string slug, int? currentUserId);
}
