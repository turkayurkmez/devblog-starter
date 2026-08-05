namespace DevBlog.Api.Services;

public record ToggleLikeResult(bool Success, int LikeCount, bool IsLikedByCurrentUser);
