using DevblogData.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// `dotnet run --project <path>` keeps the caller's working directory instead of
// switching to the project directory, so the default content root (which is based
// on the process CWD) can't be trusted to find appsettings.json. Anchor it to the
// directory the built assembly actually lives in instead.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PostTools>();

await builder.Build().RunAsync();
