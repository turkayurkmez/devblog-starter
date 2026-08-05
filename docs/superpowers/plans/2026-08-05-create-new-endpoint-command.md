# /create-new-endpoint Komutu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `.claude/commands/create-new-endpoint.md` adında, `$1` (HTTP method) ve `$2` (route) argümanlarını alan; route anlamlıysa CLAUDE.md'deki hedef mimariye (Endpoint → Service → Repository) göre adım adım plan üreten, anlamlı değilse tek tek soru sorarak ilerleyen bir proje-seviyeli slash command oluşturmak.

**Architecture:** Bu bir kod artefaktı değil, bir prompt/instruction dosyasıdır — Claude Code, komut çalıştığında bu dosyanın içeriğini sistem talimatı olarak okur. Tek dosyalık, tek görevlik bir iş; klasik unit test yerine "dry-run" doğrulaması (örnek girdilerle komut talimatlarının elle izlenmesi) kullanılacak.

**Tech Stack:** Claude Code custom slash command (Markdown + YAML frontmatter), `$1`/`$2` argüman değişkenleri.

## Global Constraints

- Dosya konumu: `.claude/commands/create-new-endpoint.md` (proje seviyesi, spec'te onaylandı).
- Komut kod yazmaz/dosya değiştirmez — yalnızca plan (veya sorular) üretir (spec: Kapsam Dışı).
- Route anlamlılık testi ve plan adımları, spec'teki `docs/superpowers/specs/2026-08-05-create-new-endpoint-command-design.md` dosyasında tanımlandığı gibi birebir uygulanacak.
- Komut içeriği Türkçe yazılacak (repodaki diğer skill dosyalarıyla — `neden-sonuc-mesaji`, `migration-guvenlik-kontrolu`, `security-audit` — tutarlı olması için).

---

### Task 1: `/create-new-endpoint` komut dosyasını oluştur ve doğrula

**Files:**
- Create: `.claude/commands/create-new-endpoint.md`

**Interfaces:**
- Consumes: yok (ilk ve tek task).
- Produces: `.claude/commands/create-new-endpoint.md` — `/create-new-endpoint <method> <route>` olarak çağrılabilen slash command.

- [ ] **Step 1: Komut dosyasını oluştur**

`.claude/commands/create-new-endpoint.md` dosyasını şu içerikle oluştur:

```markdown
---
description: Verilen HTTP method ve route için, CLAUDE.md'deki hedef mimariye (Endpoint->Service->Repository) uygun adim adim bir endpoint uygulama plani uretir; route'un anlami cikarilamiyorsa once soru sorar.
argument-hint: [http-method] [route]
---

Sen devblog-starter reposunda yeni bir backend endpoint eklemek icin plan hazirlayan bir yardimcisin. Kullanicidan gelen argumanlar:

- HTTP method: `$1`
- Route: `$2`

Asagidaki adimlari sirasiyla, atlamadan uygula.

## 1. Argüman doğrulama

- `$1` veya `$2` bos ise, plana veya route analizine GECME. Once eksik olan argumani kullaniciya sor (ornek: "Hangi route icin plan hazirlayayim?" veya "Hangi HTTP method? (GET/POST/PUT/PATCH/DELETE)"), cevabi bekle.
- `$1` GET, POST, PUT, PATCH, DELETE disinda bir degerse, kullaniciya dogru method'u sor.

## 2. Route anlamlılık testi

Route'u `{param}` segmentlerini ayiklayarak statik segmentlere bol. Ilk statik segmenti bir kaynak/varlik adina eslemeye calis:

- `backend/src/DevBlog.Api/Models` altindaki bilinen varliklarla (`Post`, `Comment`, `User`) eslesiyor mu? Emin olmak icin bu klasoru ve `backend/src/DevBlog.Api/Data/AppDbContext.cs` dosyasindaki `DbSet`leri kontrol et.
- Eslesmiyorsa, segment makul bir cogul/isim biciminde mi (yeni varlik adayi olarak kabul edilebilir mi)?

Method + route sekli asagidaki bilinen REST kaliplarindan birine uyuyor mu kontrol et:

| Kalip | Anlam |
|---|---|
| `GET /resource` | liste |
| `GET /resource/{id}` | tekil getir |
| `POST /resource` | olustur |
| `PUT` veya `PATCH /resource/{id}` | guncelle |
| `DELETE /resource/{id}` | sil |
| `GET` veya `POST /resource/{id}/subresource` | alt-kaynak (nested) aksiyonu |

**Anlamli** kabul edilme kosulu: kaynak segmenti cozumlenebiliyor VE method+route kalibi yukaridaki tabloda bir satira uyuyor.

**Anlamli degil** ise (fiil-bicimli route'lar orn. `/do-thing`, `/process`; belirsiz/kisaltilmis segmentler; method+route kombinasyonu tabloya uymuyorsa): plana GECME. Tek tek (bir mesajda bir soru) soru sorarak ilerle, ornekler:

- "Bu route hangi varlik/kaynak ile ilgili?"
- "Bu endpoint ne yapiyor (liste/olustur/guncelle/sil/ozel aksiyon)?"
- "Auth gerekiyor mu?"
- "Request body'de hangi alanlar olacak?"

Yeterli bilgi toplandiktan sonra asagidaki "3. Plan uretimi" adimina gec.

## 3. Plan üretimi

**Kod yazma, dosya olusturma/degistirme — sadece plan uret.** Once `CLAUDE.md` dosyasini oku (mimari kararlar, Technical Debt bolumu icin), ardindan ilgili mevcut dosyalari (`backend/src/DevBlog.Api/Endpoints/`, `Models/`, `Data/AppDbContext.cs`, `Program.cs`) incele ki varlik/repository/service zaten var mi yok mu bilesin.

Asagidaki adimlardan yalnizca bu endpoint icin gerekli olanlari icaren somut, dosya yolu belirten bir Markdown plani uret (var olmayan/gereksiz adimlari atla):

1. **Model/Entity** — varlik zaten varsa atla; yoksa yeni model dosyasi + `AppDbContext`'te `DbSet` + `OnModelCreating` iliski config. Yeni migration gerekiyorsa, planda `migration-guvenlik-kontrolu` skill'inin kullanilmasi gerektigini acikca belirt.
2. **Repository** — `I<Entity>Repository : IRepository<T>` arayuzu (Interface Segregation — sadece bu endpoint'in service'inin ihtiyac duydugu metodlar) + `<Entity>Repository` implementasyonu. Varligin repository'si zaten varsa, sadece eksik metodu ekle.
3. **Service** — `I<Entity>Service` arayuzu + implementasyonu; is mantigi burada, `DbContext`'e degil repository'ye bagimli.
4. **Endpoint** — `<Entity>Endpoint.Map()` icinde route handler (statik sinif yoksa yeni olusturulacagini belirt); `Program.cs`'e DI kaydi ve `Map` cagrisi; method POST/PUT/PATCH/DELETE ise `.RequireAuthorization()` uygulanip uygulanmayacagini belirt — eger emin degilsen bunu ayrica sor, varsayma.
5. **Request/Response sozlesmeleri** — DTO adlandirmasi repo konvansiyonuna uygun olsun (orn. `Create<Entity>Request`).
6. **Test notu** — CLAUDE.md Technical Debt maddesine referansla (xUnit, %70 coverage hedefi), bu endpoint icin hangi testlerin yazilmasi gerektigini belirt.
7. **Frontend notu** (yalnizca ilgiliyse) — `frontend/devblog-ui` tarafinda karsilik gelen bir servis metodu gerekip gerekmedigini belirt.

Plani, dosya yollari ve numarali adimlarla somut ve uygulamaya hazir bicimde sun. Plan sonunda kullaniciya bu plani uygulamamı ister misiniz diye sor — bu komut kendisi kod yazmaz.
```

- [ ] **Step 2: Frontmatter ve dosya konumunu doğrula**

Dosyanın `.claude/commands/create-new-endpoint.md` yolunda oluştuğunu ve YAML frontmatter'ın (`description`, `argument-hint`) geçerli olduğunu kontrol et:

Run: `powershell -Command "Get-Content '.claude/commands/create-new-endpoint.md' -TotalCount 5"`
Expected: İlk satır `---`, `description:` ve `argument-hint:` alanları görünür, ardından kapanış `---`.

- [ ] **Step 3: Dry-run doğrulama — anlamlı route**

Komut dosyasının talimatlarını, örnek girdi `$1=POST`, `$2=/posts/{slug}/comments` için elle izle (gerçek bir `/create-new-endpoint` çağrısı değil, dosyadaki mantığı doğrulama amaçlı okuma/izleme adımı):

- Route segmentleri: `posts` (statik), `{slug}` (param), `comments` (statik).
- `posts` → mevcut `Post` varlığına, `comments` alt-kaynağı → mevcut `Comment` varlığına eşleşir (ikisi de `backend/src/DevBlog.Api/Models` içinde mevcut).
- Method+route kalıbı: `POST /resource/{id}/subresource` → tabloya uyuyor.
- Sonuç: **anlamlı** kabul edilmeli → doğrudan plan adımına geçilmeli, soru sorulmamalı.

Expected: Yukarıdaki izleme, komut dosyasındaki "Anlamlı kabul edilme koşulu" ve tablo ile tutarlı sonucu (anlamlı → plana geç) üretiyor. Tutarsızlık varsa dosyayı düzelt.

- [ ] **Step 4: Dry-run doğrulama — anlamsız route**

Aynı şekilde, örnek girdi `$1=POST`, `$2=/engage` için elle izle:

- Route segmenti: `engage` — ne bilinen bir varlığa (`Post`/`Comment`/`User`) eşleşiyor ne de açık bir çoğul/isim biçiminde (fiil gibi duruyor).
- Method+route kalıbı: `POST /resource` şeklinde teknik olarak tabloya uysa da, kaynak segmenti çözümlenemiyor.
- Sonuç: **anlamlı değil** kabul edilmeli → plana geçilmemeli, "Bu route hangi varlık/kaynak ile ilgili?" gibi bir soru sorulmalı.

Expected: Komut dosyasındaki mantık bu senaryoda soru sorma dalına düşüyor (kaynak segmenti çözümlenemediği için "anlamlı" koşulu sağlanmıyor). Tutarsızlık varsa dosyayı düzelt.

- [ ] **Step 5: Commit**

```bash
git add .claude/commands/create-new-endpoint.md
git commit -m "feat: /create-new-endpoint slash command ekle"
```
