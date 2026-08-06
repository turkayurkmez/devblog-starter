# Makalelerle Sohbet Sayfası — Tasarım

## Amaç

`docs/` altındaki 12 makale zaten chunk'lanıp embed edilerek `DocChunks` tablosuna yazıldı ([DocChunk](../../../backend/src/DevBlog.Api/Models/DocChunk.cs) entity'si) ve `IDocChunkService.SearchAsync(float[] queryVector, int topK = 5)` cosine similarity ile top-K getirebiliyor. Bu tasarımın amacı, ziyaretçilerin doğal dilde soru sorup bu makalelere dayanan, akan (streaming) bir cevap alabileceği bir RAG (Retrieval-Augmented Generation) sohbet sayfası eklemektir — backend'de retrieval+generation orkestrasyonu, frontend'de canlı yazan bir sohbet arayüzü.

Kullanıcı ile netleşen kararlar:
- **LLM**: Anthropic Claude Messages API, `stream: true`.
- **Embedding**: mevcut Voyage AI entegrasyonunun (`voyage-3.5`) canlı sorgu metni için C# karşılığı.
- **Kimlik doğrulama**: yok — sayfa tüm ziyaretçilere açık.
- **Konuşma geçmişi**: yok — her soru bağımsız ele alınır, backend stateless kalır.
- **Kaynak gösterimi**: cevabın hangi makalelerden geldiği kullanıcıya gösterilir.
- **Kötüye kullanım koruması**: basit IP bazlı rate limiting.
- **API key'ler**: şimdilik placeholder; gerçek `Anthropic:ApiKey` ve `Voyage:ApiKey` değerleri kullanıcı tarafından `dotnet user-secrets` ile eklenecek, hiçbir gerçek key koda/appsettings.json'a yazılmayacak.

## Mimari Akış

```
Angular ChatComponent
   │  fetch POST /chat  { question: string }
   ▼
ChatEndpoint  (auth yok, IP bazli rate limit)
   ▼
ChatService.StreamAnswerAsync(question)
   1. IVoyageEmbeddingClient.EmbedQueryAsync(question)         → float[] (input_type="query")
   2. IDocChunkService.SearchAsync(vector, topK: 5)            → IReadOnlyList<DocChunkSearchResult>  (degismeden reuse)
   3. yield SourcesEvent(results)
   4. IAnthropicChatClient.StreamAsync(systemPrompt, question) → IAsyncEnumerable<string> (metin delta'lari)
   5. her delta icin yield DeltaEvent(text)
   6. yield DoneEvent  (basarili tamamlanma)
      | hata olursa yield ErrorEvent(mesaj)
```

`ChatService`, SSE'nin tel formatını bilmez — `ChatStreamEvent` adında domain seviyeli bir union üretir (`SourcesEvent | DeltaEvent | ErrorEvent`); SSE'ye çevirme işi `ChatEndpoint`'te yapılır. Bu ayrım, servisin test edilebilirliğini ve olası başka bir taşıma katmanına (örn. WebSocket) geçişini kolaylaştırır.

## Backend Bileşenleri

### Yeni Dosyalar

- **`Models/` değişikliği yok** — `DocChunk` zaten mevcut.
- **`Services/ChatStreamEvent.cs`**
  ```csharp
  public abstract record ChatStreamEvent;
  public record SourcesEvent(IReadOnlyList<DocChunkSearchResult> Sources) : ChatStreamEvent;
  public record DeltaEvent(string Text) : ChatStreamEvent;
  public record ErrorEvent(string Message) : ChatStreamEvent;
  ```
- **`Services/IChatService.cs` / `ChatService.cs`**
  - `IAsyncEnumerable<ChatStreamEvent> StreamAnswerAsync(string question, CancellationToken ct)`
  - Bağımlılıklar: `IVoyageEmbeddingClient`, `IDocChunkService` (mevcut), `IAnthropicChatClient`.
  - Sistem prompt'u (Anthropic'e gönderilecek, grounding için):
    > "Sen bir teknik blog asistanısın. Sadece aşağıda verilen makale alıntılarına dayanarak Türkçe cevap ver. Alıntılarda cevap yoksa, bunu açıkça söyle ve tahmin yürütme."
  - Kullanıcı mesajı: retrieved chunk içerikleri (kaynak dosya adıyla birlikte) + orijinal soru birleştirilerek oluşturulur.
  - `topK` sonuçlarından herhangi biri gelmezse (DocChunks boşsa) veya Voyage/Anthropic çağrısı başarısız olursa `ErrorEvent` üretilir, exception yutulmaz ama kullanıcıya ham hata sızdırılmaz (`ILogger` ile sunucu tarafında loglanır).
- **`Services/Clients/IVoyageEmbeddingClient.cs` / `VoyageEmbeddingClient.cs`**
  - `Task<float[]> EmbedQueryAsync(string text, CancellationToken ct)`
  - `docs/examples/embed_and_store.py` ile aynı REST çağrısı (`POST https://api.voyageai.com/v1/embeddings`), farkı `input_type="query"` (dokümanlar `"document"` ile embed edildiği için Voyage'ın query/document asimetrik embedding pratiğine uyulur) ve tek bir metin gönderilmesi.
  - `HttpClient` DI ile (`AddHttpClient<IVoyageEmbeddingClient, VoyageEmbeddingClient>()`), API key `IOptions<VoyageOptions>` üzerinden okunur.
- **`Services/Clients/IAnthropicChatClient.cs` / `AnthropicChatClient.cs`**
  - `IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userMessage, CancellationToken ct)`
  - `POST https://api.anthropic.com/v1/messages`, `stream: true`, `model` config'ten (`claude-sonnet-5` varsayılan).
  - Anthropic'in kendi SSE akışını okuyup sadece `content_block_delta` event'lerindeki metin parçalarını yield eder; `message_stop` geldiğinde döngü sonlanır.
- **`Endpoints/ChatEndpoint.cs`**
  - `app.MapPost("/chat", handler).RequireRateLimiting("chat")` — `.RequireAuthorization()` YOK.
  - Handler: `HttpContext.Response.ContentType = "text/event-stream"`; `await foreach` ile `ChatService.StreamAnswerAsync` sonuçlarını SSE frame'lerine çevirip yazar (`data: {json}\n\n`), her yazımdan sonra `FlushAsync()`.
  - SSE JSON zarfı: `{"type":"sources","sources":[...]}`, `{"type":"delta","text":"..."}`, `{"type":"error","message":"..."}`, `{"type":"done"}`.
- **`Program.cs`**
  - `builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("chat", o => { o.PermitLimit = 5; o.Window = TimeSpan.FromSeconds(60); }))` + `app.UseRateLimiter()`.
  - `AddHttpClient<IVoyageEmbeddingClient, VoyageEmbeddingClient>()`, `AddHttpClient<IAnthropicChatClient, AnthropicChatClient>()`, `AddScoped<IChatService, ChatService>()`.
  - `ChatEndpoint.Map(app)` çağrısı.
- **`appsettings.json`** (placeholder, boş string — gerçek key commit edilmez):
  ```json
  "Voyage": { "ApiKey": "", "Model": "voyage-3.5" },
  "Anthropic": { "ApiKey": "", "Model": "claude-sonnet-5" }
  ```
  Kullanıcı gerçek değerleri `dotnet user-secrets set Voyage:ApiKey "..."` / `dotnet user-secrets set Anthropic:ApiKey "..."` ile ekleyecek. `ApiKey` boşsa `ChatService` ilk çağrıda `ErrorEvent("Sohbet servisi yapılandırılmamış.")` döner (uygulama çökmez).

## Frontend Bileşenleri

- **`app.routes.ts`** — yeni route: `{ path: 'chat', loadComponent: () => import('./pages/chat/chat.component').then(m => m.ChatComponent) }` (post-list/post-detail ile aynı lazy-load standalone deseni).
- **`services/chat.service.ts`**
  - `HttpClient` yerine doğrudan `fetch()` kullanılır (streaming body okumak için en basit yol; `HttpClient`'ın varsayılan XHR backend'i parça parça okumaya uygun değil).
  - `askQuestion(question: string, handlers: { onSources, onDelta, onError, onDone })` — `fetch(environment.apiUrl + '/chat', { method: 'POST', body: JSON.stringify({ question }) })`, `response.body!.getReader()` ile okuyup `\n\n` sınırlarında SSE frame'lerini ayrıştırır, her frame'in `type` alanına göre ilgili handler'ı çağırır.
- **`pages/chat/chat.component.ts` / `.html`**
  - Soru input'u + gönder butonu.
  - Mesaj listesi: kullanıcı sorusu + asistan cevabı (delta'lar geldikçe canlı büyür) + cevabın altında küçük bir "Kaynaklar: docs/10-rag-mimarisi.md, docs/03-claude-md.md" satırı (sources event'inden).
  - Yükleniyor durumu (ilk delta gelene kadar) ve hata durumu (`ErrorEvent`/429 → "Çok fazla istek, lütfen birazdan tekrar deneyin." gibi kullanıcı dostu mesaj).
  - Geçmiş yok: her gönderim öncekinden bağımsız; önceki mesajlar sadece görsel geçmiş olarak listede kalır, backend'e tekrar gönderilmez.

## Hata Yönetimi (özet)

| Durum | Davranış |
|---|---|
| Voyage/Anthropic API hatası (429, 5xx, timeout) | `ErrorEvent` ile genel Türkçe mesaj; ham exception `ILogger` ile sunucuda loglanır, istemciye sızdırılmaz. |
| API key placeholder/boş | `ErrorEvent("Sohbet servisi yapılandırılmamış.")`, uygulama çökmez. |
| DocChunks boş/sonuç yok | `SourcesEvent([])` gönderilir, Anthropic'e "ilgili kaynak bulunamadı" notuyla devam edilir; model muhtemelen "bilmiyorum" yanıtı üretir (sistem promptu buna izin veriyor). |
| Rate limit aşıldı | ASP.NET Core `RateLimiter` middleware'i otomatik `429` döner; frontend kullanıcı dostu mesaj gösterir. |

## Test Notu

CLAUDE.md'nin test borcu maddesine göre (xUnit, backend'e meaningful dokunuşlarda test eklenmesi önerisi): `ChatService` için sahte (fake/mock) `IVoyageEmbeddingClient`, `IDocChunkService`, `IAnthropicChatClient` ile — event sırasının doğruluğu (sources → delta* → done) ve hata yolunun (`ErrorEvent`) doğru çalıştığını doğrulayan birkaç odaklı test. Kapsamlı bir test altyapısı kurmak bu planın kapsamında değil.

## Kapsam Dışı

- Konuşma geçmişi / oturum yönetimi.
- Gelişmiş rate limiting (kullanıcı bazlı kota, captcha vb.) — sadece basit IP bazlı fixed-window.
- Gerçek Anthropic/Voyage API key'lerinin eklenmesi — kullanıcı tarafından yapılacak.
- Var olan `Posts`/`Comments` endpoint'lerinde değişiklik.
