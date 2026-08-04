using DevBlog.Api.Models;

namespace DevBlog.Api.Services;

public record CreatePostResult(bool Success, string? ErrorMessage, Post? Post);
