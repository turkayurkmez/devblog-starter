namespace DevBlog.Api.Repositories;

public record DocChunkSearchResult(int Id, string SourceFile, int ChunkIndex, string Content, double Score);
