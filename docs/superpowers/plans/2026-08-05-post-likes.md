# Post Likes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Project-specific alternative:** this repo has its own `lead-orchestrator` agent (`.claude/agents/lead-orchestrator.md`) that delegates backend tasks to `backend-specialist` and frontend tasks to `frontend-specialist`. Prefer that over the generic flows above unless the user asks otherwise.

**Goal:** Let a logged-in user like/unlike a post, and let anyone see how many users liked each post.

**Architecture:** New `PostLike` join entity (`PostId` + `UserId`, unique together) tracks who liked what. Backend exposes like data through the existing `Post` slice's Repository → Service → Endpoint layers (extending `IPostRepository`/`PostService`, adding a sibling `IPostLikeRepository`), with a single `POST /posts/{slug}/likes` endpoint that toggles like/unlike for the authenticated caller. `GET /posts` gains a per-post `likeCount`; `GET /posts/{slug}` gains `likeCount` + `likedByCurrentUser` (the latter computed from the caller's JWT if present, `false` for anonymous callers — the endpoint itself stays public). Frontend adds a like button + count to the post-detail page and a read-only count to the post-list page, both fed through the existing `PostService`.

**Tech Stack:** .NET 10 Minimal API, EF Core (SQLite), Angular 22 standalone components.

## Global Constraints

- Backend target architecture is Endpoint → Service → Repository (CLAUDE.md): endpoints must not touch `AppDbContext`; only repositories may. This plan also migrates `GET /posts/{slug}` off direct `AppDbContext` access as part of adding likes to it — that endpoint was the last one in the Posts slice still on the old pattern.
- No backend test project exists in this repo, and CLAUDE.md/agent docs say not to scaffold one unless explicitly asked. Verification steps use `dotnet build backend/DevBlog.slnx` instead of a test run.
- No frontend test/lint script exists; verification steps use `npm run build` inside `frontend/devblog-ui` instead of a test run.
- ASP.NET Core minimal APIs serialize records with the default camelCase JSON policy — C# records below are written PascalCase per convention and will serialize as camelCase (`LikeCount` → `likeCount`), matching the TypeScript interfaces below.
- EF Core migrations in this repo must go through the `migration-guvenlik-kontrolu` skill, not a bare `dotnet ef migrations add` / `dotnet ef database update`.
- Naming: PascalCase for C# classes/methods/properties, camelCase for C# locals/parameters; camelCase for TypeScript variables/methods, PascalCase for TypeScript classes/interfaces.

---

### Task 1: `PostLike` entity and EF Core mapping

**Files:**
- Create: `backend/src/DevBlog.Api/Models/PostLike.cs`
- Modify: `backend/src/DevBlog.Api/Models/Post.cs:14` (add `Likes` navigation collection after `Comments`)
- Modify: `backend/src/DevBlog.Api/Data/AppDbContext.cs` (add `DbSet<PostLike>` and relationship/index config)
- Migration: generated via the `migration-guvenlik-kontrolu` skill, not written by hand

**Interfaces:**
- Produces: `PostLike { Id, PostId, Post, UserId, User, CreatedAt }`, `Post.Likes : ICollection<PostLike>`, `AppDbContext.PostLikes : DbSet<PostLike>` — consumed by Task 2's repositories.

- [ ] **Step 1: Create the `PostLike` model**

```csharp
// backend/src/DevBlog.Api/Models/PostLike.cs
namespace DevBlog.Api.Models;

public class PostLike
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Add the `Likes` navigation to `Post`**

In `backend/src/DevBlog.Api/Models/Post.cs`, add one line after the existing `Comments` property (line 14):

```csharp
public ICollection<Comment> Comments { get; set; } = [];
public ICollection<PostLike> Likes { get; set; } = [];
```

- [ ] **Step 3: Register the `DbSet` and configure the relationship in `AppDbContext`**

Replace the full contents of `backend/src/DevBlog.Api/Data/AppDbContext.cs` with:

```csharp
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>()
            .HasIndex(p => p.Slug)
            .IsUnique();

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId);

        modelBuilder.Entity<PostLike>()
            .HasIndex(l => new { l.PostId, l.UserId })
            .IsUnique();

        modelBuilder.Entity<PostLike>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId);

        modelBuilder.Entity<PostLike>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId);
    }
}
```

The unique index on `(PostId, UserId)` is what makes "one like per user per post" a database-level guarantee, not just an application-level assumption.

- [ ] **Step 4: Verify it builds**

Run: `dotnet build backend/DevBlog.slnx`
Expected: Build succeeds (the model/DbContext changes alone don't need a migration to compile).

- [ ] **Step 5: Create and apply the migration via the migration safety skill**

Invoke the `migration-guvenlik-kontrolu` skill to generate a migration named `AddPostLikes` for this change (new table, new unique index — no existing columns are altered, so this should be low-risk, but the skill's approval gate still applies) and apply it with `dotnet ef database update` only after it approves.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DevBlog.Api/Models/PostLike.cs backend/src/DevBlog.Api/Models/Post.cs backend/src/DevBlog.Api/Data/AppDbContext.cs backend/src/DevBlog.Api/Migrations/
git commit -m "feat: add PostLike entity and EF Core mapping"
```

---

### Task 2: Repository layer — `IPostLikeRepository` and `IPostRepository` extensions

**Files:**
- Create: `backend/src/DevBlog.Api/Repositories/IPostLikeRepository.cs`
- Create: `backend/src/DevBlog.Api/Repositories/PostLikeRepository.cs`
- Create: `backend/src/DevBlog.Api/Repositories/PostDetailItem.cs`
- Modify: `backend/src/DevBlog.Api/Repositories/PostListItem.cs` (append `LikeCount`)
- Modify: `backend/src/DevBlog.Api/Repositories/IPostRepository.cs` (add two methods)
- Modify: `backend/src/DevBlog.Api/Repositories/PostRepository.cs` (implement the two methods, update `GetPagedAsync` projection)

**Interfaces:**
- Consumes: `PostLike`, `Post.Likes`, `AppDbContext.PostLikes` (Task 1); `Repository<T>` base (`AnyAsync`, `AddAsync`, `SaveChangesAsync`, protected `Db`) — unchanged, already exists.
- Produces: `PostListItem(Id, Title, Slug, Tags, PublishedAt, ReadingInMinutes, Author, LikeCount)`, `PostDetailItem(Id, Title, Content, Slug, Tags, PublishedAt, ReadingInMinutes, Author, Comments)`, `PostCommentItem(Id, AuthorName, Body, CreatedAt)`, `IPostRepository.GetDetailBySlugAsync(string slug) : Task<PostDetailItem?>`, `IPostRepository.GetIdBySlugAsync(string slug) : Task<int?>`, `IPostLikeRepository.FindByPostAndUserAsync(int postId, int userId) : Task<PostLike?>`, `IPostLikeRepository.CountByPostAsync(int postId) : Task<int>`, `IPostLikeRepository.Remove(PostLike like) : void` — consumed by Task 3's `PostService`.

- [ ] **Step 1: Add `LikeCount` to `PostListItem`**

```csharp
// backend/src/DevBlog.Api/Repositories/PostListItem.cs
namespace DevBlog.Api.Repositories;

public record PostListItem(int Id, string Title, string Slug, string Tags, DateTime PublishedAt, int ReadingInMinutes, string Author, int LikeCount);
```

- [ ] **Step 2: Create `PostDetailItem` and `PostCommentItem`**

```csharp
// backend/src/DevBlog.Api/Repositories/PostDetailItem.cs
namespace DevBlog.Api.Repositories;

public record PostDetailItem(
    int Id,
    string Title,
    string Content,
    string Slug,
    string Tags,
    DateTime PublishedAt,
    int ReadingInMinutes,
    string Author,
    IReadOnlyList<PostCommentItem> Comments);

public record PostCommentItem(int Id, string AuthorName, string Body, DateTime CreatedAt);
```

- [ ] **Step 3: Extend `IPostRepository`**

```csharp
// backend/src/DevBlog.Api/Repositories/IPostRepository.cs
using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IPostRepository : IRepository<Post>
{
    Task<PagedResult<PostListItem>> GetPagedAsync(int page, int pageSize, string? tag);
    Task<PostDetailItem?> GetDetailBySlugAsync(string slug);
    Task<int?> GetIdBySlugAsync(string slug);
}
```

- [ ] **Step 4: Implement the new `PostRepository` methods and update `GetPagedAsync`**

Replace the full contents of `backend/src/DevBlog.Api/Repositories/PostRepository.cs` with:

```csharp
using DevBlog.Api.Data;
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class PostRepository(AppDbContext db) : Repository<Post>(db), IPostRepository
{
    public async Task<PagedResult<PostListItem>> GetPagedAsync(int page, int pageSize, string? tag)
    {
        var query = Db.Posts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var escapedTag = tag.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(p =>
                EF.Functions.Like(p.Tags, escapedTag, "\\") ||
                EF.Functions.Like(p.Tags, escapedTag + ",%", "\\") ||
                EF.Functions.Like(p.Tags, "%," + escapedTag, "\\") ||
                EF.Functions.Like(p.Tags, "%," + escapedTag + ",%", "\\"));
        }

        query = query.OrderByDescending(p => p.PublishedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostListItem(p.Id, p.Title, p.Slug, p.Tags, p.PublishedAt, p.ReadingInMinutes, p.Author.Username, p.Likes.Count))
            .ToListAsync();

        return new PagedResult<PostListItem>(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public Task<PostDetailItem?> GetDetailBySlugAsync(string slug) =>
        Db.Posts
            .AsNoTracking()
            .Where(p => p.Slug == slug)
            .Select(p => new PostDetailItem(
                p.Id,
                p.Title,
                p.Content,
                p.Slug,
                p.Tags,
                p.PublishedAt,
                p.ReadingInMinutes,
                p.Author.Username,
                p.Comments.OrderBy(c => c.CreatedAt)
                    .Select(c => new PostCommentItem(c.Id, c.AuthorName, c.Body, c.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync();

    public Task<int?> GetIdBySlugAsync(string slug) =>
        Db.Posts.Where(p => p.Slug == slug).Select(p => (int?)p.Id).FirstOrDefaultAsync();
}
```

- [ ] **Step 5: Create `IPostLikeRepository`**

```csharp
// backend/src/DevBlog.Api/Repositories/IPostLikeRepository.cs
using DevBlog.Api.Models;

namespace DevBlog.Api.Repositories;

public interface IPostLikeRepository : IRepository<PostLike>
{
    Task<PostLike?> FindByPostAndUserAsync(int postId, int userId);
    Task<int> CountByPostAsync(int postId);
    void Remove(PostLike like);
}
```

- [ ] **Step 6: Implement `PostLikeRepository`**

```csharp
// backend/src/DevBlog.Api/Repositories/PostLikeRepository.cs
using DevBlog.Api.Data;
using DevBlog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class PostLikeRepository(AppDbContext db) : Repository<PostLike>(db), IPostLikeRepository
{
    public Task<PostLike?> FindByPostAndUserAsync(int postId, int userId) =>
        Db.PostLikes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

    public Task<int> CountByPostAsync(int postId) =>
        Db.PostLikes.CountAsync(l => l.PostId == postId);

    public void Remove(PostLike like) => Db.PostLikes.Remove(like);
}
```

Note: `FindByPostAndUserAsync` deliberately does not use `AsNoTracking()` — the result may be passed straight into `Remove()` in Task 3, and EF needs it tracked (or at least key-populated) for that to work cleanly.

- [ ] **Step 7: Verify it builds**

Run: `dotnet build backend/DevBlog.slnx`
Expected: Build fails at this point — `IPostService`/`PostService` don't yet implement anything using these new repository members, but nothing calls them yet either, so it should actually succeed. Confirm it does.

- [ ] **Step 8: Commit**

```bash
git add backend/src/DevBlog.Api/Repositories/
git commit -m "feat: add PostLike repository and extend post repository for like data"
```

---

### Task 3: Service layer — `GetPostDetailAsync` and `ToggleLikeAsync`

**Files:**
- Create: `backend/src/DevBlog.Api/Services/PostDetailResult.cs`
- Create: `backend/src/DevBlog.Api/Services/LikeToggleResult.cs`
- Modify: `backend/src/DevBlog.Api/Services/IPostService.cs`
- Modify: `backend/src/DevBlog.Api/Services/PostService.cs`

**Interfaces:**
- Consumes: `IPostRepository.GetDetailBySlugAsync`, `GetIdBySlugAsync` and `IPostLikeRepository.FindByPostAndUserAsync`, `CountByPostAsync`, `Remove`, `AddAsync`, `SaveChangesAsync` (Task 2); `PostCommentItem` (Task 2, reused directly — no separate service-level comment type).
- Produces: `IPostService.GetPostDetailAsync(string slug, int? currentUserId) : Task<PostDetailResult?>`, `IPostService.ToggleLikeAsync(string slug, int userId) : Task<LikeToggleResult?>` — consumed by Task 4's endpoints.

- [ ] **Step 1: Create `PostDetailResult`**

```csharp
// backend/src/DevBlog.Api/Services/PostDetailResult.cs
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public record PostDetailResult(
    int Id,
    string Title,
    string Content,
    string Slug,
    string Tags,
    DateTime PublishedAt,
    int ReadingInMinutes,
    string Author,
    int LikeCount,
    bool LikedByCurrentUser,
    IReadOnlyList<PostCommentItem> Comments);
```

- [ ] **Step 2: Create `LikeToggleResult`**

```csharp
// backend/src/DevBlog.Api/Services/LikeToggleResult.cs
namespace DevBlog.Api.Services;

public record LikeToggleResult(int LikeCount, bool LikedByCurrentUser);
```

- [ ] **Step 3: Extend `IPostService`**

```csharp
// backend/src/DevBlog.Api/Services/IPostService.cs
using DevBlog.Api.Endpoints;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public interface IPostService
{
    Task<CreatePostResult> CreatePostAsync(CreatePostRequest request, int authorId);
    Task<PagedResult<PostListItem>> GetPostsAsync(int page, int pageSize, string? tag);
    Task<PostDetailResult?> GetPostDetailAsync(string slug, int? currentUserId);
    Task<LikeToggleResult?> ToggleLikeAsync(string slug, int userId);
}
```

- [ ] **Step 4: Implement both methods in `PostService`**

Replace the full contents of `backend/src/DevBlog.Api/Services/PostService.cs` with:

```csharp
using DevBlog.Api.Endpoints;
using DevBlog.Api.Models;
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public class PostService(IPostRepository postRepository, IPostLikeRepository postLikeRepository) : IPostService
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
            ReadingInMinutes = ReadingTimeEstimator.EstimateMinutes(request.Content),
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

    public async Task<PostDetailResult?> GetPostDetailAsync(string slug, int? currentUserId)
    {
        var post = await postRepository.GetDetailBySlugAsync(slug);
        if (post is null)
        {
            return null;
        }

        var likeCount = await postLikeRepository.CountByPostAsync(post.Id);
        var likedByCurrentUser = currentUserId is not null
            && await postLikeRepository.FindByPostAndUserAsync(post.Id, currentUserId.Value) is not null;

        return new PostDetailResult(
            post.Id, post.Title, post.Content, post.Slug, post.Tags,
            post.PublishedAt, post.ReadingInMinutes, post.Author,
            likeCount, likedByCurrentUser, post.Comments);
    }

    public async Task<LikeToggleResult?> ToggleLikeAsync(string slug, int userId)
    {
        var postId = await postRepository.GetIdBySlugAsync(slug);
        if (postId is null)
        {
            return null;
        }

        var existingLike = await postLikeRepository.FindByPostAndUserAsync(postId.Value, userId);
        bool likedByCurrentUser;

        if (existingLike is not null)
        {
            postLikeRepository.Remove(existingLike);
            likedByCurrentUser = false;
        }
        else
        {
            await postLikeRepository.AddAsync(new PostLike
            {
                PostId = postId.Value,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            likedByCurrentUser = true;
        }

        await postLikeRepository.SaveChangesAsync();

        var likeCount = await postLikeRepository.CountByPostAsync(postId.Value);

        return new LikeToggleResult(likeCount, likedByCurrentUser);
    }
}
```

`ToggleLikeAsync` is the whole toggle behavior: if the user already liked the post, this call removes the like; otherwise it adds one. The response always reflects the state *after* the toggle, which is what the frontend button needs to flip its own label.

- [ ] **Step 5: Verify it builds**

Run: `dotnet build backend/DevBlog.slnx`
Expected: Build fails — nothing constructs `PostService` with the new `IPostLikeRepository` parameter yet (that's Task 4's `Program.cs` change). This is expected at this point in the plan; Task 4 closes the gap.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DevBlog.Api/Services/
git commit -m "feat: add post detail and like toggle to PostService"
```

---

### Task 4: Endpoint layer and DI registration

**Files:**
- Modify: `backend/src/DevBlog.Api/Endpoints/PostsEndpoint.cs`
- Modify: `backend/src/DevBlog.Api/Program.cs:44` (register `IPostLikeRepository`)

**Interfaces:**
- Consumes: `IPostService.GetPostDetailAsync`, `ToggleLikeAsync` (Task 3); `IPostLikeRepository`/`PostLikeRepository` (Task 2, for DI registration).
- Produces: `GET /posts/{slug}` response body matches `PostDetailResult` (camelCase JSON: `id, title, content, slug, tags, publishedAt, readingInMinutes, author, likeCount, likedByCurrentUser, comments: [{id, authorName, body, createdAt}]`); `POST /posts/{slug}/likes` response body matches `LikeToggleResult` (`likeCount, likedByCurrentUser`) — consumed by Task 5's frontend `PostService`.

- [ ] **Step 1: Rewrite `PostsEndpoint.cs`**

Replace the full contents of `backend/src/DevBlog.Api/Endpoints/PostsEndpoint.cs` with:

```csharp
using System.Security.Claims;
using DevBlog.Api.Services;

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

        app.MapGet("/posts/{slug}", async (string slug, IPostService postService, ClaimsPrincipal user) =>
        {
            int? currentUserId = user.Identity?.IsAuthenticated == true
                ? int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            var result = await postService.GetPostDetailAsync(slug, currentUserId);

            return result is null ? Results.NotFound() : Results.Ok(result);
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

        app.MapPost("/posts/{slug}/likes", async (string slug, IPostService postService, ClaimsPrincipal user) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await postService.ToggleLikeAsync(slug, userId);

            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();
    }
}

public record CreatePostRequest(string Title, string Content, string Slug, string Tags);
```

This also removes `PostsEndpoint`'s last direct `AppDbContext`/EF Core usage (the old `GET /posts/{slug}` handler) — the whole Posts slice is now consistently Endpoint → Service → Repository. `GET /posts/{slug}` stays anonymous-accessible: `ClaimsPrincipal` is always bound from `HttpContext.User` regardless of `.RequireAuthorization()`, and `UseAuthentication()` (already in `Program.cs`) populates it from any valid Bearer token even on endpoints that don't require one — so a logged-in caller still gets `likedByCurrentUser` computed correctly, and an anonymous caller gets `false` without error.

- [ ] **Step 2: Register `IPostLikeRepository` in `Program.cs`**

In `backend/src/DevBlog.Api/Program.cs`, change lines 43-45 from:

```csharp
// 6. Repositories & Services
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostService>();
```

to:

```csharp
// 6. Repositories & Services
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostLikeRepository, PostLikeRepository>();
builder.Services.AddScoped<IPostService, PostService>();
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build backend/DevBlog.slnx`
Expected: Build succeeds. This is the point where Tasks 1-4 are all wired together correctly.

- [ ] **Step 4: Manual smoke test**

Run: `dotnet run --project backend/src/DevBlog.Api/DevBlog.Api.csproj`
Then, from another terminal (replace `<port>` with whatever Kestrel bound to in the console output, and `<token>` with the value from a `POST /auth/login` call using the seeded admin credentials):

```bash
curl -X POST http://localhost:<port>/posts/<some-existing-slug>/likes -H "Authorization: Bearer <token>"
curl http://localhost:<port>/posts/<some-existing-slug>
```

Expected: first call returns `{"likeCount":1,"likedByCurrentUser":true}`; second call's JSON includes `"likeCount":1,"likedByCurrentUser":true` (or `false` if called without the `Authorization` header). Calling the first `curl` again should flip it back to `likeCount:0, likedByCurrentUser:false`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/DevBlog.Api/Endpoints/PostsEndpoint.cs backend/src/DevBlog.Api/Program.cs
git commit -m "feat: expose like toggle and like data through posts endpoints"
```

---

### Task 5: Frontend `PostService` — models and `toggleLike`

**Files:**
- Modify: `frontend/devblog-ui/src/app/services/post.service.ts`

**Interfaces:**
- Consumes: `GET /posts` (now returns `likeCount` per item), `GET /posts/{slug}` (now returns `likeCount`, `likedByCurrentUser`), `POST /posts/{slug}/likes` (Task 4).
- Produces: `PostSummary.likeCount: number`, `PostDetail.likedByCurrentUser: boolean`, `PostService.toggleLike(slug: string) : Observable<LikeStatus>`, `LikeStatus { likeCount: number; likedByCurrentUser: boolean }` — consumed by Task 6 and Task 7.

- [ ] **Step 1: Add the fields and method**

Replace the full contents of `frontend/devblog-ui/src/app/services/post.service.ts` with:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface PostSummary {
  id: number;
  title: string;
  slug: string;
  tags: string;
  publishedAt: string;
  author: string;
  likeCount: number;
}

export interface PostDetail extends PostSummary {
  content: string;
  comments: Comment[];
  likedByCurrentUser: boolean;
}

export interface Comment {
  id: number;
  authorName: string;
  body: string;
  createdAt: string;
}

export interface LikeStatus {
  likeCount: number;
  likedByCurrentUser: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class PostService {
  private http = inject(HttpClient);

  getPosts(page = 1, pageSize = 10) {
    return this.http.get<PagedResult<PostSummary>>(`${environment.apiUrl}/posts`, {
      params: { page, pageSize }
    });
  }

  getPost(slug: string) {
    return this.http.get<PostDetail>(`${environment.apiUrl}/posts/${slug}`);
  }

  createPost(data: { title: string; content: string; slug: string; tags: string }) {
    return this.http.post(`${environment.apiUrl}/posts`, data);
  }

  addComment(slug: string, data: { authorName: string; body: string }) {
    return this.http.post(`${environment.apiUrl}/posts/${slug}/comments`, data);
  }

  toggleLike(slug: string) {
    return this.http.post<LikeStatus>(`${environment.apiUrl}/posts/${slug}/likes`, {});
  }
}
```

- [ ] **Step 2: Verify it builds**

Run: `cd frontend/devblog-ui && npm run build`
Expected: Build succeeds (nothing consumes the new fields/method yet, so there's nothing to break — `PostSummary`/`PostDetail` are structurally widened, which is backward compatible with existing template bindings).

- [ ] **Step 3: Commit**

```bash
git add frontend/devblog-ui/src/app/services/post.service.ts
git commit -m "feat: add like count and toggle-like to PostService"
```

---

### Task 6: Post-list UI — show like count

**Files:**
- Modify: `frontend/devblog-ui/src/app/pages/post-list/post-list.component.html`

**Interfaces:**
- Consumes: `PostSummary.likeCount` (Task 5).

- [ ] **Step 1: Add the like count next to each post**

Replace the full contents of `frontend/devblog-ui/src/app/pages/post-list/post-list.component.html` with:

```html
<h1>Posts</h1>
<ul>
  @for (post of posts; track post.id) {
    <li>
      <a [routerLink]="['/posts', post.slug]">{{ post.title }}</a>
      <small> — {{ post.author }} | {{ post.publishedAt | date:'mediumDate' }} | ❤ {{ post.likeCount }}</small>
      <br>
      <em>{{ post.tags }}</em>
    </li>
  }
</ul>

<div>
  <button (click)="goToPage(page - 1)" [disabled]="page <= 1">Önceki</button>
  <span> Sayfa {{ page }} / {{ totalPages }} </span>
  <button (click)="goToPage(page + 1)" [disabled]="page >= totalPages">Sonraki</button>
</div>
```

No `.ts` change is needed here — `post-list.component.ts` already exposes `posts: PostSummary[]`, and `PostSummary` gained `likeCount` in Task 5.

- [ ] **Step 2: Verify it builds**

Run: `cd frontend/devblog-ui && npm run build`
Expected: Build succeeds.

- [ ] **Step 3: Manual check**

Run: `npm start` (inside `frontend/devblog-ui`, with the backend from Task 4 running), open the post list page in a browser.
Expected: each post row shows a `❤ <count>` next to the author/date.

- [ ] **Step 4: Commit**

```bash
git add frontend/devblog-ui/src/app/pages/post-list/post-list.component.html
git commit -m "feat: show like count in post list"
```

---

### Task 7: Post-detail UI — like button

**Files:**
- Modify: `frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.ts`
- Modify: `frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.html`

**Interfaces:**
- Consumes: `PostDetail.likeCount`, `PostDetail.likedByCurrentUser`, `PostService.toggleLike` (Task 5); `AuthService.isLoggedIn()` (existing, `frontend/devblog-ui/src/app/services/auth.service.ts:29`).

- [ ] **Step 1: Add like-toggle logic to the component**

Replace the full contents of `frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.ts` with:

```typescript
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PostService, PostDetail } from '../../services/post.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-post-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './post-detail.component.html'
})
export class PostDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);
  private postService = inject(PostService);
  authService = inject(AuthService);

  post: PostDetail | null = null;
  commentAuthor = '';
  commentBody = '';
  submitted = false;

  ngOnInit() {
    const slug = this.route.snapshot.paramMap.get('slug')!;
    this.postService.getPost(slug).subscribe(p => {
      this.post = p;
      this.cdr.detectChanges(); //bu satır, değişiklikleri algılamak ve bileşeni güncellemek için ChangeDetectorRef kullanır

    } );
  }

  submitComment() {
    if (!this.post) return;
    this.postService
      .addComment(this.post.slug, { authorName: this.commentAuthor, body: this.commentBody })
      .subscribe(() => {
        this.submitted = true;
        this.commentAuthor = '';
        this.commentBody = '';
        const slug = this.route.snapshot.paramMap.get('slug')!;
        this.postService.getPost(slug).subscribe(p => (this.post = p));
      });
  }

  toggleLike() {
    if (!this.post) return;

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.postService.toggleLike(this.post.slug).subscribe(status => {
      if (!this.post) return;
      this.post.likeCount = status.likeCount;
      this.post.likedByCurrentUser = status.likedByCurrentUser;
      this.cdr.detectChanges();
    });
  }
}
```

Not-logged-in users still see the like button and count (per the feature request, anyone can see how many likes a post has); clicking it while logged out redirects to `/login` instead of calling the API, since `POST /posts/{slug}/likes` requires auth. This reuses `AuthService.isLoggedIn()`, which already exists but was previously unused by any guard or component.

- [ ] **Step 2: Add the like button to the template**

Replace the full contents of `frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.html` with:

```html
@if (post) {
  <article>
    <h1>{{ post.title }}</h1>
    <p><strong>{{ post.author }}</strong> | {{ post.publishedAt | date:'mediumDate' }} | {{ post.tags }}</p>
    <p>
      <button (click)="toggleLike()">
        {{ post.likedByCurrentUser ? 'Beğenildi ✓' : 'Beğen' }}
      </button>
      {{ post.likeCount }} beğeni
    </p>
    <div>{{ post.content }}</div>
  </article>

  <section>
    <h2>Comments</h2>
    @for (c of post.comments; track c.id) {
      <div>
        <strong>{{ c.authorName }}</strong> <small>{{ c.createdAt | date:'short' }}</small>
        <p>{{ c.body }}</p>
      </div>
    }

    <h3>Add a comment</h3>
    @if (submitted) {
      <p>Comment submitted!</p>
    }
    <form (ngSubmit)="submitComment()">
      <input [(ngModel)]="commentAuthor" name="author" placeholder="Your name" required>
      <textarea [(ngModel)]="commentBody" name="body" placeholder="Your comment" required></textarea>
      <button type="submit">Submit</button>
    </form>
  </section>
} @else {
  <p>Loading...</p>
}
```

- [ ] **Step 3: Verify it builds**

Run: `cd frontend/devblog-ui && npm run build`
Expected: Build succeeds.

- [ ] **Step 4: Manual check**

Run: `npm start` (with the backend running), open a post detail page.
Expected: logged out — button reads "Beğen", clicking it navigates to `/login`. Log in, return to the post — clicking "Beğen" flips it to "Beğenildi ✓" and increments the count; clicking again flips it back and decrements the count. Refreshing the page preserves the liked state (served from `GET /posts/{slug}`).

- [ ] **Step 5: Commit**

```bash
git add frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.ts frontend/devblog-ui/src/app/pages/post-detail/post-detail.component.html
git commit -m "feat: add like button to post detail page"
```
