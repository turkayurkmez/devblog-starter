using DevBlog.Api.Endpoints;
using DevBlog.Api.Models;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public class PostService(IPostRepository postRepository) : IPostService
{
    public async Task<CreatePostResult> CreatePostAsync(CreatePostRequest request, int authorId)
    {
        if (await postRepository.AnyAsync(p => p.Slug == request.Slug))
        {
            return new CreatePostResult(false, "Bu slug zaten kullanılıyor.", null);
        }

        var post = new Post
        {
            Title = request.Title,
            Content = request.Content,
            Slug = request.Slug,
            Tags = request.Tags,
            PublishedAt = DateTime.UtcNow,
            AuthorId = authorId
        };

        await postRepository.AddAsync(post);
        await postRepository.SaveChangesAsync();

        return new CreatePostResult(true, null, post);
    }

    public Task<PagedResult<PostListItem>> GetPostsAsync(int page, int pageSize, string? tag)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedTag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();

        return postRepository.GetPagedAsync(page, pageSize, normalizedTag);
    }
}
