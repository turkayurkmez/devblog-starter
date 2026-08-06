namespace DevBlog.Api.Models;

public class DocChunk
{
    public int Id { get; set; }
    public string SourceFile { get; set; } = "";
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = "";
    public string VectorJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
