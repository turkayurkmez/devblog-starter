using System.Security.Claims;
using DevBlog.Api.Data;
using DevBlog.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Endpoints;

public static class PostsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/posts", async (IPostService postService, int page = 1, int pageSize = 10, string? tag = null) =>
        {
            var result = await postService.GetPostsAsync(page, pageSize, tag);

            return Results.Ok(new
            {
                items = result.Items,
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            });
        });

        app.MapGet("/posts/{slug}", async (string slug, AppDbContext db) =>
        {
            var post = await db.Posts
                .Include(p => p.Author)
                .Include(p => p.Comments)
                .Where(p => p.Slug == slug)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Content,
                    p.Slug,
                    p.Tags,
                    p.PublishedAt,
                    p.ReadingInMinutes,
                    Author = p.Author.Username,
                    Comments = p.Comments.OrderBy(c => c.CreatedAt).Select(c => new
                    {
                        c.Id,
                        c.AuthorName,
                        c.Body,
                        c.CreatedAt
                    })
                })
                .FirstOrDefaultAsync();

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
}

public record CreatePostRequest(string Title, string Content, string Slug, string Tags);
