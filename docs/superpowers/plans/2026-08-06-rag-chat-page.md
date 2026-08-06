# Makalelerle Sohbet Sayfası Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ziyaretçilerin `docs/` altındaki makalelere dayanarak doğal dilde soru sorabildiği, akan (streaming) cevap alabildiği, kimlik doğrulaması gerektirmeyen bir sohbet sayfası eklemek.

**Architecture:** Backend'de `ChatEndpoint → ChatService → (VoyageEmbeddingClient + DocChunkService + AnthropicChatClient)` orkestrasyonu, sonucu SSE (`text/event-stream`) ile frontend'e akıtır. Frontend'de `/chat` route'unda standalone bir Angular component, `fetch()` + `ReadableStream` ile SSE'yi tüketip cevabı canlı gösterir. Konuşma geçmişi yok — her istek bağımsız, backend stateless.

**Tech Stack:** .NET 10 Minimal API, EF Core/SQLite (mevcut `DocChunk`), Anthropic Messages API (streaming), Voyage AI Embeddings API, Angular 22 standalone components, xUnit (backend testleri).

## Global Constraints

- Gerçek Anthropic/Voyage API key'leri koda veya `appsettings.json`'a yazılmaz — sadece boş placeholder; kullanıcı kendi key'lerini `dotnet user-secrets` ile ekleyecek.
- `/chat` endpoint'i kimlik doğrulaması GEREKTİRMEZ (`.RequireAuthorization()` çağrılmaz), sadece IP bazlı rate limiting (`5 istek / 60 saniye`).
- Konuşma geçmişi tutulmaz — her `/chat` isteği bağımsızdır.
- Cevapla birlikte kullanılan kaynak makaleler (`SourcesEvent`) frontend'e gösterilir.
- Mevcut Endpoint → Service → Repository mimarisi ve dosya/isimlendirme konvansiyonları (`backend/src/DevBlog.Api/{Endpoints,Services,Repositories}`) izlenir.
- Referans spec: `docs/superpowers/specs/2026-08-06-rag-chat-page-design.md`.

---

### Task 1: xUnit test projesi + Voyage embedding client

**Files:**
- Create: `backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
- Create: `backend/tests/DevBlog.Api.Tests/Fakes/FakeHttpMessageHandler.cs`
- Create: `backend/src/DevBlog.Api/Services/Clients/VoyageOptions.cs`
- Create: `backend/src/DevBlog.Api/Services/Clients/IVoyageEmbeddingClient.cs`
- Create: `backend/src/DevBlog.Api/Services/Clients/VoyageEmbeddingClient.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/Clients/VoyageEmbeddingClientTests.cs`
- Modify: `backend/DevBlog.slnx`
- Modify: `backend/src/DevBlog.Api/appsettings.json`

**Interfaces:**
- Produces: `IVoyageEmbeddingClient.EmbedQueryAsync(string text, CancellationToken ct = default) : Task<float[]>`, `VoyageOptions { ApiKey, Model }` (`IOptions<VoyageOptions>` ile bind edilir, section adı `VoyageOptions.SectionName = "Voyage"`).
- Produces: `FakeHttpMessageHandler` — Task 2 ve Task 3'teki testlerde de reuse edilecek.

- [ ] **Step 1: xUnit test projesini oluştur**

```bash
dotnet new xunit -n DevBlog.Api.Tests -o backend/tests/DevBlog.Api.Tests --framework net10.0
dotnet add backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj reference backend/src/DevBlog.Api/DevBlog.Api.csproj
rm backend/tests/DevBlog.Api.Tests/UnitTest1.cs
```

`backend/DevBlog.slnx`'i şu hale getir:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/DevBlog.Api/DevBlog.Api.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Build ile projenin tanındığını doğrula**

Run: `dotnet build backend/DevBlog.slnx`
Expected: `Build succeeded` (yeni test projesi dahil, 0 Error).

- [ ] **Step 3: Paylaşılan `FakeHttpMessageHandler`'ı yaz**

`backend/tests/DevBlog.Api.Tests/Fakes/FakeHttpMessageHandler.cs`:

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DevBlog.Api.Tests.Fakes;

public class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));
}
```

- [ ] **Step 4: Voyage embedding client için başarısız (henüz yok) testi yaz**

`backend/tests/DevBlog.Api.Tests/Services/Clients/VoyageEmbeddingClientTests.cs`:

```csharp
using System.Net;
using DevBlog.Api.Services.Clients;
using DevBlog.Api.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevBlog.Api.Tests.Services.Clients;

public class VoyageEmbeddingClientTests
{
    [Fact]
    public async Task EmbedQueryAsync_ParsesEmbeddingFromResponse()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"embedding":[0.1,0.2,0.3]}]}""")
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new VoyageOptions { ApiKey = "test-key", Model = "voyage-3.5" });
        var client = new VoyageEmbeddingClient(httpClient, options);

        var result = await client.EmbedQueryAsync("agentic loop nedir?");

        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f }, result);
    }

    [Fact]
    public async Task EmbedQueryAsync_ThrowsWhenApiKeyMissing()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new VoyageOptions { ApiKey = "", Model = "voyage-3.5" });
        var client = new VoyageEmbeddingClient(httpClient, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.EmbedQueryAsync("soru"));
    }
}
```

- [ ] **Step 5: Testleri çalıştırıp derleme hatasıyla başarısız olduğunu doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: derleme hatası — `VoyageOptions`, `IVoyageEmbeddingClient`, `VoyageEmbeddingClient` bulunamıyor.

- [ ] **Step 6: `VoyageOptions` ve `IVoyageEmbeddingClient`'ı yaz**

`backend/src/DevBlog.Api/Services/Clients/VoyageOptions.cs`:

```csharp
namespace DevBlog.Api.Services.Clients;

public class VoyageOptions
{
    public const string SectionName = "Voyage";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "voyage-3.5";
}
```

`backend/src/DevBlog.Api/Services/Clients/IVoyageEmbeddingClient.cs`:

```csharp
namespace DevBlog.Api.Services.Clients;

public interface IVoyageEmbeddingClient
{
    Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default);
}
```

- [ ] **Step 7: `VoyageEmbeddingClient`'ı yaz**

`backend/src/DevBlog.Api/Services/Clients/VoyageEmbeddingClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DevBlog.Api.Services.Clients;

public class VoyageEmbeddingClient(HttpClient httpClient, IOptions<VoyageOptions> options) : IVoyageEmbeddingClient
{
    private readonly VoyageOptions _options = options.Value;

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Voyage:ApiKey yapılandırılmamış.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.voyageai.com/v1/embeddings")
        {
            Content = JsonContent.Create(new VoyageEmbedRequest([text], _options.Model, "query"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<VoyageEmbedResponse>(cancellationToken: ct);
        return body!.Data[0].Embedding;
    }

    private record VoyageEmbedRequest(
        [property: JsonPropertyName("input")] string[] Input,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input_type")] string InputType);

    private record VoyageEmbedResponse([property: JsonPropertyName("data")] VoyageEmbedData[] Data);

    private record VoyageEmbedData([property: JsonPropertyName("embedding")] float[] Embedding);
}
```

- [ ] **Step 8: Testleri çalıştırıp geçtiğini doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 2`.

- [ ] **Step 9: `appsettings.json`'a Voyage placeholder'ını ekle**

`backend/src/DevBlog.Api/appsettings.json` içeriğini şu hale getir:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=devblog.db"
  },
  "Voyage": {
    "ApiKey": "",
    "Model": "voyage-3.5"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 10: Commit**

```bash
git add backend/tests/DevBlog.Api.Tests backend/DevBlog.slnx backend/src/DevBlog.Api/Services/Clients backend/src/DevBlog.Api/appsettings.json
git commit -m "feat: Voyage embedding client icin xUnit test projesi ve VoyageEmbeddingClient ekle"
```

---

### Task 2: Anthropic chat client (streaming)

**Files:**
- Create: `backend/src/DevBlog.Api/Services/Clients/AnthropicOptions.cs`
- Create: `backend/src/DevBlog.Api/Services/Clients/IAnthropicChatClient.cs`
- Create: `backend/src/DevBlog.Api/Services/Clients/AnthropicChatClient.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/Clients/AnthropicChatClientTests.cs`
- Modify: `backend/src/DevBlog.Api/appsettings.json`

**Interfaces:**
- Consumes: `FakeHttpMessageHandler` (Task 1).
- Produces: `IAnthropicChatClient.StreamAsync(string systemPrompt, string userMessage, CancellationToken ct = default) : IAsyncEnumerable<string>`, `AnthropicOptions { ApiKey, Model }` (section adı `AnthropicOptions.SectionName = "Anthropic"`).

- [ ] **Step 1: Testi yaz**

`backend/tests/DevBlog.Api.Tests/Services/Clients/AnthropicChatClientTests.cs`:

```csharp
using System.Net;
using DevBlog.Api.Services.Clients;
using DevBlog.Api.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevBlog.Api.Tests.Services.Clients;

public class AnthropicChatClientTests
{
    private const string SseBody =
        "event: content_block_delta\n" +
        "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Merhaba\"}}\n\n" +
        "event: content_block_delta\n" +
        "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\" dunya\"}}\n\n" +
        "event: message_stop\n" +
        "data: {\"type\":\"message_stop\"}\n\n";

    [Fact]
    public async Task StreamAsync_YieldsTextDeltasInOrder()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SseBody)
        });
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AnthropicOptions { ApiKey = "test-key", Model = "claude-sonnet-5" });
        var client = new AnthropicChatClient(httpClient, options);

        var deltas = new List<string>();
        await foreach (var delta in client.StreamAsync("system prompt", "soru"))
        {
            deltas.Add(delta);
        }

        Assert.Equal(["Merhaba", " dunya"], deltas);
    }

    [Fact]
    public async Task StreamAsync_ThrowsWhenApiKeyMissing()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new AnthropicOptions { ApiKey = "", Model = "claude-sonnet-5" });
        var client = new AnthropicChatClient(httpClient, options);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.StreamAsync("system", "soru")) { }
        });
    }
}
```

- [ ] **Step 2: Testi çalıştırıp derleme hatasıyla başarısız olduğunu doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: derleme hatası — `AnthropicOptions`, `IAnthropicChatClient`, `AnthropicChatClient` bulunamıyor.

- [ ] **Step 3: `AnthropicOptions` ve arayüzü yaz**

`backend/src/DevBlog.Api/Services/Clients/AnthropicOptions.cs`:

```csharp
namespace DevBlog.Api.Services.Clients;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-5";
}
```

`backend/src/DevBlog.Api/Services/Clients/IAnthropicChatClient.cs`:

```csharp
namespace DevBlog.Api.Services.Clients;

public interface IAnthropicChatClient
{
    IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
```

- [ ] **Step 4: `AnthropicChatClient`'ı yaz**

`backend/src/DevBlog.Api/Services/Clients/AnthropicChatClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DevBlog.Api.Services.Clients;

public class AnthropicChatClient(HttpClient httpClient, IOptions<AnthropicOptions> options) : IAnthropicChatClient
{
    private readonly AnthropicOptions _options = options.Value;

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Anthropic:ApiKey yapılandırılmamış.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            max_tokens = 1024,
            system = systemPrompt,
            stream = true,
            messages = new[] { new { role = "user", content = userMessage } }
        });

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || !line.StartsWith("data: "))
            {
                continue;
            }

            var json = line["data: ".Length..];
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();

            if (type == "content_block_delta")
            {
                var text = doc.RootElement.GetProperty("delta").GetProperty("text").GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
            else if (type == "message_stop")
            {
                yield break;
            }
        }
    }
}
```

- [ ] **Step 5: Testleri çalıştırıp geçtiğini doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 6: `appsettings.json`'a Anthropic placeholder'ını ekle**

`backend/src/DevBlog.Api/appsettings.json`'daki `"Voyage"` bloğunun altına ekle:

```json
  "Anthropic": {
    "ApiKey": "",
    "Model": "claude-sonnet-5"
  },
```

(Tam dosya: `ConnectionStrings` → `Voyage` → `Anthropic` → `Logging` → `AllowedHosts` sırasıyla.)

- [ ] **Step 7: Commit**

```bash
git add backend/src/DevBlog.Api/Services/Clients backend/tests/DevBlog.Api.Tests/Services/Clients/AnthropicChatClientTests.cs backend/src/DevBlog.Api/appsettings.json
git commit -m "feat: Anthropic Messages API icin streaming AnthropicChatClient ekle"
```

---

### Task 3: `ChatStreamEvent` + `ChatService`

**Files:**
- Create: `backend/src/DevBlog.Api/Services/ChatStreamEvent.cs`
- Create: `backend/src/DevBlog.Api/Services/IChatService.cs`
- Create: `backend/src/DevBlog.Api/Services/ChatService.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeVoyageEmbeddingClient.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeDocChunkService.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeAnthropicChatClient.cs`
- Create: `backend/tests/DevBlog.Api.Tests/Services/ChatServiceTests.cs`

**Interfaces:**
- Consumes: `IVoyageEmbeddingClient.EmbedQueryAsync` (Task 1), `IAnthropicChatClient.StreamAsync` (Task 2), mevcut `IDocChunkService.SearchAsync(float[] queryVector, int topK = 5) : Task<IReadOnlyList<DocChunkSearchResult>>` ([DocChunkService.cs](../../../backend/src/DevBlog.Api/Services/DocChunkService.cs)).
- Produces: `IChatService.StreamAnswerAsync(string question, CancellationToken ct = default) : IAsyncEnumerable<ChatStreamEvent>`; `ChatStreamEvent` alt tipleri `SourcesEvent(IReadOnlyList<DocChunkSearchResult> Sources)`, `DeltaEvent(string Text)`, `ErrorEvent(string Message)` — Task 4 (`ChatEndpoint`) bunları tüketecek.

- [ ] **Step 1: Fake test double'larını yaz**

`backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeVoyageEmbeddingClient.cs`:

```csharp
using DevBlog.Api.Services.Clients;

namespace DevBlog.Api.Tests.Services.Fakes;

public class FakeVoyageEmbeddingClient : IVoyageEmbeddingClient
{
    public float[] ReturnVector { get; set; } = [1f, 0f];
    public Exception? ThrowOnEmbed { get; set; }

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default)
    {
        if (ThrowOnEmbed is not null)
        {
            throw ThrowOnEmbed;
        }

        return Task.FromResult(ReturnVector);
    }
}
```

`backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeDocChunkService.cs`:

```csharp
using DevBlog.Api.Repositories;
using DevBlog.Api.Services;

namespace DevBlog.Api.Tests.Services.Fakes;

public class FakeDocChunkService : IDocChunkService
{
    public IReadOnlyList<DocChunkSearchResult> ReturnResults { get; set; } = [];

    public Task<IReadOnlyList<DocChunkSearchResult>> SearchAsync(float[] queryVector, int topK = 5) =>
        Task.FromResult(ReturnResults);
}
```

`backend/tests/DevBlog.Api.Tests/Services/Fakes/FakeAnthropicChatClient.cs`:

```csharp
using System.Runtime.CompilerServices;
using DevBlog.Api.Services.Clients;

namespace DevBlog.Api.Tests.Services.Fakes;

public class FakeAnthropicChatClient : IAnthropicChatClient
{
    public string[] Deltas { get; set; } = [];

    public async IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var delta in Deltas)
        {
            yield return delta;
            await Task.Yield();
        }
    }
}
```

- [ ] **Step 2: `ChatServiceTests`'i yaz**

`backend/tests/DevBlog.Api.Tests/Services/ChatServiceTests.cs`:

```csharp
using DevBlog.Api.Repositories;
using DevBlog.Api.Services;
using DevBlog.Api.Tests.Services.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevBlog.Api.Tests.Services;

public class ChatServiceTests
{
    [Fact]
    public async Task StreamAnswerAsync_YieldsSourcesThenDeltas()
    {
        var embeddingClient = new FakeVoyageEmbeddingClient();
        var docChunkService = new FakeDocChunkService
        {
            ReturnResults = [new DocChunkSearchResult(1, "docs/01-agentic-loop.md", 0, "içerik", 0.9)]
        };
        var anthropicClient = new FakeAnthropicChatClient { Deltas = ["Merhaba", " dünya"] };
        var chatService = new ChatService(embeddingClient, docChunkService, anthropicClient, NullLogger<ChatService>.Instance);

        var events = new List<ChatStreamEvent>();
        await foreach (var evt in chatService.StreamAnswerAsync("agentic loop nedir?"))
        {
            events.Add(evt);
        }

        Assert.Collection(events,
            evt => Assert.IsType<SourcesEvent>(evt),
            evt => Assert.Equal("Merhaba", Assert.IsType<DeltaEvent>(evt).Text),
            evt => Assert.Equal(" dünya", Assert.IsType<DeltaEvent>(evt).Text));
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenEmbeddingFails_YieldsSingleErrorEvent()
    {
        var embeddingClient = new FakeVoyageEmbeddingClient { ThrowOnEmbed = new InvalidOperationException("Voyage:ApiKey yapılandırılmamış.") };
        var docChunkService = new FakeDocChunkService();
        var anthropicClient = new FakeAnthropicChatClient();
        var chatService = new ChatService(embeddingClient, docChunkService, anthropicClient, NullLogger<ChatService>.Instance);

        var events = new List<ChatStreamEvent>();
        await foreach (var evt in chatService.StreamAnswerAsync("soru"))
        {
            events.Add(evt);
        }

        Assert.Collection(events, evt => Assert.IsType<ErrorEvent>(evt));
    }
}
```

- [ ] **Step 3: Testleri çalıştırıp derleme hatasıyla başarısız olduğunu doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: derleme hatası — `ChatStreamEvent`, `SourcesEvent`, `DeltaEvent`, `ErrorEvent`, `ChatService` bulunamıyor.

- [ ] **Step 4: `ChatStreamEvent.cs`'i yaz**

`backend/src/DevBlog.Api/Services/ChatStreamEvent.cs`:

```csharp
using DevBlog.Api.Repositories;

namespace DevBlog.Api.Services;

public abstract record ChatStreamEvent;

public record SourcesEvent(IReadOnlyList<DocChunkSearchResult> Sources) : ChatStreamEvent;

public record DeltaEvent(string Text) : ChatStreamEvent;

public record ErrorEvent(string Message) : ChatStreamEvent;
```

- [ ] **Step 5: `IChatService.cs`'i yaz**

`backend/src/DevBlog.Api/Services/IChatService.cs`:

```csharp
namespace DevBlog.Api.Services;

public interface IChatService
{
    IAsyncEnumerable<ChatStreamEvent> StreamAnswerAsync(string question, CancellationToken ct = default);
}
```

- [ ] **Step 6: `ChatService.cs`'i yaz**

`backend/src/DevBlog.Api/Services/ChatService.cs`:

```csharp
using System.Runtime.CompilerServices;
using DevBlog.Api.Repositories;
using DevBlog.Api.Services.Clients;
using Microsoft.Extensions.Logging;

namespace DevBlog.Api.Services;

public class ChatService(
    IVoyageEmbeddingClient embeddingClient,
    IDocChunkService docChunkService,
    IAnthropicChatClient anthropicChatClient,
    ILogger<ChatService> logger) : IChatService
{
    private const string SystemPromptTemplate =
        "Sen bir teknik blog asistanısın. Sadece aşağıda verilen makale alıntılarına dayanarak Türkçe cevap ver. " +
        "Alıntılarda cevap yoksa, bunu açıkça söyle ve tahmin yürütme.\n\nMakale alıntıları:\n{0}";

    public async IAsyncEnumerable<ChatStreamEvent> StreamAnswerAsync(
        string question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        IReadOnlyList<DocChunkSearchResult> sources;
        try
        {
            var queryVector = await embeddingClient.EmbedQueryAsync(question, ct);
            sources = await docChunkService.SearchAsync(queryVector);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat retrieval basarisiz.");
            yield return new ErrorEvent("Sohbet servisi şu anda kullanılamıyor.");
            yield break;
        }

        yield return new SourcesEvent(sources);

        var context = string.Join("\n\n", sources.Select(s => $"[{s.SourceFile}]\n{s.Content}"));
        var systemPrompt = string.Format(SystemPromptTemplate, context);

        await foreach (var delta in anthropicChatClient.StreamAsync(systemPrompt, question, ct))
        {
            yield return new DeltaEvent(delta);
        }
    }
}
```

- [ ] **Step 7: Testleri çalıştırıp geçtiğini doğrula**

Run: `dotnet test backend/tests/DevBlog.Api.Tests/DevBlog.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 8: Commit**

```bash
git add backend/src/DevBlog.Api/Services backend/tests/DevBlog.Api.Tests/Services
git commit -m "feat: RAG orkestrasyonu icin ChatStreamEvent ve ChatService ekle"
```

---

### Task 4: `ChatEndpoint` + `Program.cs` wiring (rate limiting, DI)

**Files:**
- Create: `backend/src/DevBlog.Api/Endpoints/ChatEndpoint.cs`
- Modify: `backend/src/DevBlog.Api/Program.cs`

**Interfaces:**
- Consumes: `IChatService.StreamAnswerAsync` (Task 3), `SourcesEvent`/`DeltaEvent`/`ErrorEvent` (Task 3).
- Produces: `POST /chat` — request body `{ "question": string }`, yanıt `text/event-stream`, frame'ler `data: {"type":"sources"|"delta"|"error"|"done", ...}\n\n`.

- [ ] **Step 1: `ChatEndpoint.cs`'i yaz**

`backend/src/DevBlog.Api/Endpoints/ChatEndpoint.cs`:

```csharp
using System.Text.Json;
using DevBlog.Api.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace DevBlog.Api.Endpoints;

public static class ChatEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        app.MapPost("/chat", async (ChatRequest req, IChatService chatService, HttpContext httpContext, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question))
            {
                return Results.BadRequest(new { message = "question boş olamaz." });
            }

            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";

            try
            {
                await foreach (var evt in chatService.StreamAnswerAsync(req.Question, ct))
                {
                    await WriteEventAsync(httpContext, evt, ct);
                }

                await WriteRawAsync(httpContext, """{"type":"done"}""", ct);
            }
            catch (Exception)
            {
                await WriteRawAsync(
                    httpContext,
                    """{"type":"error","message":"Sohbet servisi şu anda kullanılamıyor."}""",
                    ct);
            }

            return Results.Empty;
        }).RequireRateLimiting("chat");
    }

    private static Task WriteEventAsync(HttpContext httpContext, ChatStreamEvent evt, CancellationToken ct)
    {
        var json = evt switch
        {
            SourcesEvent s => JsonSerializer.Serialize(new { type = "sources", sources = s.Sources }, JsonOptions),
            DeltaEvent d => JsonSerializer.Serialize(new { type = "delta", text = d.Text }, JsonOptions),
            ErrorEvent e => JsonSerializer.Serialize(new { type = "error", message = e.Message }, JsonOptions),
            _ => throw new InvalidOperationException("Bilinmeyen ChatStreamEvent tipi.")
        };
        return WriteRawAsync(httpContext, json, ct);
    }

    private static async Task WriteRawAsync(HttpContext httpContext, string json, CancellationToken ct)
    {
        await httpContext.Response.WriteAsync($"data: {json}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }
}

public record ChatRequest(string Question);
```

- [ ] **Step 2: `Program.cs`'e DI, rate limiter ve `Map` çağrısını ekle**

`backend/src/DevBlog.Api/Program.cs` başındaki `using` bloğuna ekle:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DevBlog.Api.Services.Clients;
```

`// 6. Repositories & Services` bloğunun sonuna (`AddScoped<IDocChunkService, DocChunkService>();` satırından sonra) ekle:

```csharp
builder.Services.Configure<VoyageOptions>(builder.Configuration.GetSection(VoyageOptions.SectionName));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));
builder.Services.AddHttpClient<IVoyageEmbeddingClient, VoyageEmbeddingClient>();
builder.Services.AddHttpClient<IAnthropicChatClient, AnthropicChatClient>();
builder.Services.AddScoped<IChatService, ChatService>();

builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("chat", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromSeconds(60);
    }));
```

`app.UseAuthorization();` satırından sonra ekle:

```csharp
app.UseRateLimiter();
```

`CommentsEndpoint.Map(app);` satırından sonra ekle:

```csharp
ChatEndpoint.Map(app);
```

- [ ] **Step 3: Build'in geçtiğini doğrula**

Run: `dotnet build backend/DevBlog.slnx`
Expected: `Build succeeded`, 0 Error.

- [ ] **Step 4: Uygulamayı çalıştırıp uçtan uca (key'siz) davranışı doğrula**

```bash
dotnet run --project backend/src/DevBlog.Api/DevBlog.Api.csproj
```

Konsolda gerçek dinleme adresini not al (örn. `http://localhost:5231`), ayrı bir terminalde:

```bash
curl -N -X POST http://localhost:5231/chat -H "Content-Type: application/json" -d "{\"question\":\"Agentic loop nedir?\"}"
```

Expected: `data: {"type":"error","message":"Sohbet servisi şu anda kullanılamıyor."}` (Voyage/Anthropic key'leri henüz placeholder olduğu için — bu, tüm wiring'in (rate limiter, DI, endpoint, SSE yazımı) doğru çalıştığını, sadece gerçek API çağrısının key eksikliğinden düştüğünü kanıtlar).

Ayrıca boş soru testi:

```bash
curl -i -X POST http://localhost:5231/chat -H "Content-Type: application/json" -d "{\"question\":\"\"}"
```

Expected: `400 Bad Request`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/DevBlog.Api/Endpoints/ChatEndpoint.cs backend/src/DevBlog.Api/Program.cs
git commit -m "feat: POST /chat SSE endpoint'ini ve rate limiting/DI wiring'ini ekle"
```

---

### Task 5: Frontend `ChatService` (SSE tüketimi)

**Files:**
- Create: `frontend/devblog-ui/src/app/services/chat.service.ts`

**Interfaces:**
- Consumes: Backend `POST /chat` SSE sözleşmesi (Task 4): `data: {"type":"sources","sources":[{"id":number,"sourceFile":string,"chunkIndex":number,"content":string,"score":number}]}`, `data: {"type":"delta","text":string}`, `data: {"type":"error","message":string}`, `data: {"type":"done"}`.
- Produces: `ChatService.askQuestion(question: string, handlers: ChatEventHandlers, signal?: AbortSignal): Promise<void>` — Task 6'daki `ChatComponent` tarafından kullanılacak.

- [ ] **Step 1: `chat.service.ts`'i yaz**

`frontend/devblog-ui/src/app/services/chat.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

export interface ChatSource {
  id: number;
  sourceFile: string;
  chunkIndex: number;
  content: string;
  score: number;
}

export interface ChatEventHandlers {
  onSources: (sources: ChatSource[]) => void;
  onDelta: (text: string) => void;
  onError: (message: string) => void;
  onDone: () => void;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  async askQuestion(question: string, handlers: ChatEventHandlers, signal?: AbortSignal): Promise<void> {
    let response: Response;
    try {
      response = await fetch(`${environment.apiUrl}/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question }),
        signal
      });
    } catch {
      handlers.onError('Sohbet servisine ulaşılamadı.');
      return;
    }

    if (!response.ok || !response.body) {
      handlers.onError('Sohbet servisine ulaşılamadı.');
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const frames = buffer.split('\n\n');
      buffer = frames.pop() ?? '';

      for (const frame of frames) {
        const line = frame.trim();
        if (!line.startsWith('data: ')) {
          continue;
        }

        const payload = JSON.parse(line.slice('data: '.length));
        switch (payload.type) {
          case 'sources':
            handlers.onSources(payload.sources);
            break;
          case 'delta':
            handlers.onDelta(payload.text);
            break;
          case 'error':
            handlers.onError(payload.message);
            break;
          case 'done':
            handlers.onDone();
            break;
        }
      }
    }
  }
}
```

- [ ] **Step 2: Derlemenin geçtiğini doğrula**

Run: `cd frontend/devblog-ui && npm run build`
Expected: derleme hatasız tamamlanır (henüz hiçbir component bu servisi kullanmıyor, sadece derlenebilir olması yeterli).

- [ ] **Step 3: Commit**

```bash
git add frontend/devblog-ui/src/app/services/chat.service.ts
git commit -m "feat: /chat SSE akisini tuketen ChatService'i frontend'e ekle"
```

---

### Task 6: Frontend `/chat` sayfası + route + uçtan uca doğrulama

**Files:**
- Create: `frontend/devblog-ui/src/app/pages/chat/chat.component.ts`
- Create: `frontend/devblog-ui/src/app/pages/chat/chat.component.html`
- Modify: `frontend/devblog-ui/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `ChatService.askQuestion` (Task 5).

- [ ] **Step 1: `chat.component.ts`'i yaz**

`frontend/devblog-ui/src/app/pages/chat/chat.component.ts`:

```typescript
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService, ChatSource } from '../../services/chat.service';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  sources: ChatSource[];
  error: string | null;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html'
})
export class ChatComponent {
  private chatService = inject(ChatService);
  private cdr = inject(ChangeDetectorRef);

  question = '';
  messages: ChatMessage[] = [];
  isStreaming = false;

  async send() {
    const question = this.question.trim();
    if (!question || this.isStreaming) {
      return;
    }

    this.messages.push({ role: 'user', text: question, sources: [], error: null });
    const assistantMessage: ChatMessage = { role: 'assistant', text: '', sources: [], error: null };
    this.messages.push(assistantMessage);

    this.question = '';
    this.isStreaming = true;
    this.cdr.detectChanges();

    await this.chatService.askQuestion(question, {
      onSources: (sources) => {
        assistantMessage.sources = sources;
        this.cdr.detectChanges();
      },
      onDelta: (text) => {
        assistantMessage.text += text;
        this.cdr.detectChanges();
      },
      onError: (message) => {
        assistantMessage.error = message;
        this.isStreaming = false;
        this.cdr.detectChanges();
      },
      onDone: () => {
        this.isStreaming = false;
        this.cdr.detectChanges();
      }
    });

    this.isStreaming = false;
    this.cdr.detectChanges();
  }
}
```

- [ ] **Step 2: `chat.component.html`'i yaz**

`frontend/devblog-ui/src/app/pages/chat/chat.component.html`:

```html
<h1 class="mb-4">Makalelerle Sohbet</h1>

<div class="mb-4">
  @for (message of messages; track $index) {
    <div class="card mb-3" [class.border-danger]="message.error">
      <div class="card-body">
        <div class="text-muted small mb-1">{{ message.role === 'user' ? 'Siz' : 'Asistan' }}</div>

        @if (message.error) {
          <div class="alert alert-danger mb-0" role="alert">{{ message.error }}</div>
        } @else {
          <p class="card-text mb-2" style="white-space: pre-wrap">{{ message.text }}</p>

          @if (message.sources.length > 0) {
            <div class="text-muted small">
              Kaynaklar:
              @for (source of message.sources; track source.id) {
                <span class="badge bg-secondary me-1">{{ source.sourceFile }}</span>
              }
            </div>
          }
        }
      </div>
    </div>
  }
</div>

<form class="d-flex gap-2" (ngSubmit)="send()">
  <input
    type="text"
    class="form-control"
    placeholder="Makaleler hakkında bir soru sorun..."
    [(ngModel)]="question"
    name="question"
    [disabled]="isStreaming"
    required>
  <button type="submit" class="btn btn-primary" [disabled]="isStreaming || !question.trim()">
    {{ isStreaming ? 'Yanıtlanıyor...' : 'Gönder' }}
  </button>
</form>
```

- [ ] **Step 3: Route'u ekle**

`frontend/devblog-ui/src/app/app.routes.ts`'deki `routes` dizisine, `login` route'undan sonra ekle:

```typescript
  {
    path: 'chat',
    loadComponent: () =>
      import('./pages/chat/chat.component').then(m => m.ChatComponent)
  }
```

- [ ] **Step 4: Build'in geçtiğini doğrula**

Run: `cd frontend/devblog-ui && npm run build`
Expected: derleme hatasız tamamlanır.

- [ ] **Step 5: Tarayıcıda uçtan uca (key'siz) doğrulama**

1. Backend'i çalıştır: `dotnet run --project backend/src/DevBlog.Api/DevBlog.Api.csproj` (dinlediği portu not al).
2. `frontend/devblog-ui/src/environments/environment.development.ts`'deki `apiUrl`'in bu porta işaret ettiğini doğrula.
3. Frontend'i çalıştır: `cd frontend/devblog-ui && npm start`.
4. Tarayıcıda `http://localhost:4200/chat`'e git.
5. Bir soru yazıp "Gönder"e bas.
6. **Beklenen (key'ler henüz placeholder olduğu için):** kullanıcı mesajı listeye eklenir, asistan balonu belirir ve kısa süre sonra kırmızı bir hata kutusunda "Sohbet servisi şu anda kullanılamıyor." mesajı görünür — sayfa çökmez, konsolda yakalanmamış hata olmaz. Bu, frontend↔backend↔SSE zincirinin uçtan uca doğru kurulduğunu kanıtlar.
7. Gerçek Anthropic/Voyage API key'lerini `dotnet user-secrets set Voyage:ApiKey "..."` ve `dotnet user-secrets set Anthropic:ApiKey "..."` ile ekledikten sonra aynı adımları tekrarla; bu kez cevabın kelime kelime canlı yazıldığını ve altında kaynak makale rozetlerinin göründüğünü doğrula.

- [ ] **Step 6: Commit**

```bash
git add frontend/devblog-ui/src/app/pages/chat frontend/devblog-ui/src/app/app.routes.ts
git commit -m "feat: /chat sayfasini ve route'unu frontend'e ekle"
```
