# devblog-data

.NET 10 / C# ile yazilmis, Devblog backend'inin SQLite veritabanina (`devblog.db`) `Microsoft.Data.Sqlite` ile dogrudan baglanan (EF Core / Service katmani yok) bir MCP server.

## Tool'lar

- **get_posts** — parametre almaz, `Posts` tablosundaki tum makaleleri `id`, `title`, `slug`, `publishedAt` alanlariyla, yayin tarihine gore azalan sirada dondurur.

## Baglanti dizesi

`appsettings.json` icindeki `ConnectionStrings:DevblogDb` degeri, backend projesindeki `devblog.db` dosyasina goreli bir yol icerir:

```json
"Data Source=../../backend/src/DevBlog.Api/devblog.db"
```

Bu yol, `dotnet run` bu proje klasorunden (`mcp-servers/DevblogData`) calistirildiginda dogru cozumlenir. Farkli bir calisma dizininden calistiriyorsaniz asagidaki gibi ortam degiskeniyle gecersiz kilabilirsiniz:

```bash
ConnectionStrings__DevblogDb="Data Source=C:/tam/yol/devblog.db"
```

## Build

```bash
cd mcp-servers/DevblogData
dotnet build
```

## Claude Code'a Ekleme

```bash
claude mcp add devblog-data -- dotnet run --project "mcp-servers/DevblogData/DevblogData.csproj" --no-build
```

Yonetim:

```bash
claude mcp list
claude mcp remove devblog-data
```

## Manuel calistirma / test

```bash
dotnet run
```

Process stdin uzerinden JSON-RPC mesaji bekler; dogrudan terminalden okunabilir bir cikti vermez. `get_posts` cagrisini test etmek icin MCP Inspector veya bir MCP client kullanin.
