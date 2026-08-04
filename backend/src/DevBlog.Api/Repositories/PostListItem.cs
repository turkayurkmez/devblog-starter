namespace DevBlog.Api.Repositories;

public record PostListItem(int Id, string Title, string Slug, string Tags, DateTime PublishedAt, string Author);
