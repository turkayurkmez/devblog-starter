using DevBlog.Api.Endpoints;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public interface IPostService
{
    Task<CreatePostResult> CreatePostAsync(CreatePostRequest request, int authorId);
    Task<PagedResult<PostListItem>> GetPostsAsync(int page, int pageSize, string? tag, int? currentUserId);
    Task<PostDetail?> GetPostBySlugAsync(string slug, int? currentUserId);
}
