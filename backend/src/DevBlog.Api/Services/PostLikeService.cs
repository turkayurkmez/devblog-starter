using DevBlog.Api.Models;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public class PostLikeService(IPostRepository postRepository, IPostLikeRepository postLikeRepository) : IPostLikeService
{
    public async Task<ToggleLikeResult> ToggleLikeAsync(string slug, int userId)
    {
        var post = await postRepository.GetBySlugAsync(slug);
        if (post is null)
        {
            return new ToggleLikeResult(false, 0, false);
        }

        var existingLike = await postLikeRepository.GetAsync(post.Id, userId);
        bool isLiked;

        if (existingLike is null)
        {
            await postLikeRepository.AddAsync(new PostLike
            {
                PostId = post.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            isLiked = true;
        }
        else
        {
            postLikeRepository.Remove(existingLike);
            isLiked = false;
        }

        await postLikeRepository.SaveChangesAsync();

        var likeCount = await postLikeRepository.CountAsync(post.Id);

        return new ToggleLikeResult(true, likeCount, isLiked);
    }
}
