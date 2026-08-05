using DevBlog.Api.Data;
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class PostLikeRepository(AppDbContext db) : Repository<PostLike>(db), IPostLikeRepository
{
    public Task<PostLike?> GetAsync(int postId, int userId) =>
        Db.PostLikes.FirstOrDefaultAsync(pl => pl.PostId == postId && pl.UserId == userId);

    public Task<int> CountAsync(int postId) =>
        Db.PostLikes.CountAsync(pl => pl.PostId == postId);
}
