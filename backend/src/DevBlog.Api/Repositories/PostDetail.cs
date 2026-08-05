namespace DevBlog.Api.Repositories;

public record PostDetail(
    int Id,
    string Title,
    string Content,
    string Slug,
    string Tags,
    DateTime PublishedAt,
    int ReadingInMinutes,
    string Author,
    int LikeCount,
    bool IsLikedByCurrentUser,
    IReadOnlyList<PostCommentItem> Comments);

public record PostCommentItem(int Id, string AuthorName, string Body, DateTime CreatedAt);
