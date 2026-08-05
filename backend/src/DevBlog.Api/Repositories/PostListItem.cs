namespace DevBlog.Api.Repositories;

public record PostListItem(int Id, string Title, string Slug, string Tags, DateTime PublishedAt, int ReadingInMinutes, string Author, int LikeCount, bool IsLikedByCurrentUser);
