using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace DevblogData.Tools;

[McpServerToolType]
public class PostTools(IConfiguration configuration)
{
    // The MCP client can launch this process from any working directory, so relative
    // paths in the connection string can't be resolved against the process CWD.
    // Anchor them to this project's own directory instead, found by walking up from
    // the built assembly's location until DevblogData.csproj is found.
    private static readonly string ProjectDirectory = FindProjectDirectory();

    [McpServerTool(Name = "get_posts")]
    [Description("Devblog veritabanindaki tum makaleleri (id, title, slug, publishedAt) dondurur.")]
    public async Task<IReadOnlyList<PostSummary>> GetPosts(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DevblogDb")
            ?? throw new InvalidOperationException("ConnectionStrings:DevblogDb tanimli degil.");

        var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (!Path.IsPathRooted(connectionStringBuilder.DataSource))
        {
            connectionStringBuilder.DataSource = Path.GetFullPath(
                Path.Combine(ProjectDirectory, connectionStringBuilder.DataSource));
        }

        await using var connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, Slug, PublishedAt FROM Posts ORDER BY PublishedAt DESC";

        var posts = new List<PostSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            posts.Add(new PostSummary(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3)));
        }

        return posts;
    }

    private static string FindProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DevblogData.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("DevblogData.csproj bulunamadi; proje dizini tespit edilemedi.");
    }
}

public record PostSummary(int Id, string Title, string Slug, DateTime PublishedAt);
