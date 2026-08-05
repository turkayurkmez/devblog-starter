using System.Security.Claims;
using DevBlog.Api.Services;

namespace DevBlog.Api.Endpoints;

public static class PostsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/posts", async (IPostService postService, ClaimsPrincipal user, int page = 1, int pageSize = 10, string? tag = null) =>
        {
            var currentUserId = GetCurrentUserId(user);
            var result = await postService.GetPostsAsync(page, pageSize, tag, currentUserId);

            return Results.Ok(new
            {
                items = result.Items,
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            });
        });

        app.MapGet("/posts/{slug}", async (string slug, IPostService postService, ClaimsPrincipal user) =>
        {
            var currentUserId = GetCurrentUserId(user);
            var post = await postService.GetPostBySlugAsync(slug, currentUserId);

            return post is null ? Results.NotFound() : Results.Ok(post);
        });

        app.MapPost("/posts", async (CreatePostRequest req, IPostService postService, ClaimsPrincipal user) =>
        {
            var authorId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await postService.CreatePostAsync(req, authorId);

            if (!result.Success)
            {
                return Results.Conflict(new { message = result.ErrorMessage });
            }

            return Results.Created($"/posts/{result.Post!.Slug}", new { result.Post.Id, result.Post.Slug });
        }).RequireAuthorization();
    }

    /// <summary>Resolves the caller's user id from the JWT if a valid, authenticated principal is present; null for anonymous requests.</summary>
    private static int? GetCurrentUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
}

public record CreatePostRequest(string Title, string Content, string Slug, string Tags);
