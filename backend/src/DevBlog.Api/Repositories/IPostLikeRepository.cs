using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IPostLikeRepository : IRepository<PostLike>
{
    Task<PostLike?> GetAsync(int postId, int userId);
    Task<int> CountAsync(int postId);
}
