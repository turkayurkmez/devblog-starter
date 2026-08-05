using System.Security.Claims;
using DevBlog.Api.Services;

namespace DevBlog.Api.Endpoints;

public static class PostLikesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/posts/{slug}/like", async (string slug, IPostLikeService postLikeService, ClaimsPrincipal user) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await postLikeService.ToggleLikeAsync(slug, userId);

            if (!result.Success)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { likeCount = result.LikeCount, isLikedByCurrentUser = result.IsLikedByCurrentUser });
        }).RequireAuthorization();
    }
}
