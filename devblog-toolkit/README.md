# devblog-toolkit

DevBlog reposuna (`.NET 10` minimal API `DevBlog.Api` + Angular 22 `devblog-ui`) ozgu Claude Code bilesenlerini tek plugin altinda toplar. Daha once repo icinde `.claude/skills`, `.claude/commands` ve `.claude/agents` altinda dagitik duran skill/command/agent tanimlari buraya tasinmistir.

## Icerik

### Skills (`skills/`)

- **migration-guvenlik-kontrolu** — EF Core / SQLite migration'larini (`dotnet ef migrations add`, `dotnet ef database update`) sessiz veri kaybina karsi denetler; migration olusturulmadan once ve `Up()` metodu uygulanmadan once devreye girer.
- **neden-sonuc-mesaji** — Commit mesajlari ve code review aciklamalari icin Turkce, neden-sonuc iliskisi kuran metinler uretir.
- **security-audit** — Backend endpoint'lerini (`PostsEndpoint`, `CommentsEndpoint`, `AuthEndpoint`) ve `Program.cs`/frontend auth akisini OWASP Top 10 ve bu repoya ozgu senaryolar acisindan denetler; kod degistirmez, yalnizca rapor uretir.
- **seo-audit-skill** — `devblog-ui` (Angular 22, CSR-only) icin route/component bazinda SEO denetimi yapar (meta tag, baslik hiyerarsisi, SSR/prerender durumu).

### Commands (`commands/`)

- **create-new-endpoint** — Verilen HTTP metodu ve route icin CLAUDE.md'deki katmanli mimariye (Endpoint -> Service -> Repository) uygun yeni bir endpoint'i planlayip adim adim uygular.

### Agents (`agents/`)

- **backend-specialist** — `DevBlog.Api` (.NET 10 minimal API, EF Core/SQLite) kapsamli gorevler icin.
- **frontend-specialist** — `devblog-ui` (Angular 22 standalone component) kapsamli gorevler icin.
- **lead-orchestrator** — Hem backend hem frontend'i ilgilendiren gorevleri iki uzman agent'a dagitip sonuclari birlestirir; kod yazmaz, yalnizca delege eder.

## Yerel test

Repo kokunden:

```
claude --plugin-dir ./devblog-toolkit
```

Ya da yerel marketplace olarak eklemek icin:

```
/plugin marketplace add ./devblog-toolkit
/plugin install devblog-toolkit@devblog-toolkit-marketplace
```

## Not

Bu plugin DevBlog reposuna ozgudur; genel amacli veya baska projelerde kullanilmasi beklenmez.
