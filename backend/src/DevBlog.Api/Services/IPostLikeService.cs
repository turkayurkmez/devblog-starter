namespace DevBlog.Api.Services;

public interface IPostLikeService
{
    /// <summary>Likes the post on behalf of the user if not already liked, otherwise unlikes it.</summary>
    Task<ToggleLikeResult> ToggleLikeAsync(string slug, int userId);
}
