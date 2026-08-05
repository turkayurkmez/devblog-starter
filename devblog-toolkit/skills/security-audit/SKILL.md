---
name: security-audit
description: >
  devblog-starter reposuna özel: backend endpoint'lerinin (Endpoints/PostsEndpoint.cs,
  CommentsEndpoint.cs, AuthEndpoint.cs) ve ilgili Program.cs/frontend auth akışının OWASP
  Top 10, input validation, CORS ve bu repoya özgü diğer güvenlik senaryoları açısından
  denetimini yapar. Kullanıcı "güvenlik denetimi yap", "bu endpoint'i güvenlik açısından
  incele", "OWASP kontrolü yap", "security review", "bu route'u pentest bak" gibi bir istekte
  bulunduğunda ya da yeni/değişen bir endpoint (auth, veri yazma, kullanıcı girdisi alma)
  eklendiğinde MUTLAKA bu skill'i kullan. Skill kod değiştirmez, yalnızca bulgu/öneri/severity
  formatında rapor üretir.
---

# Security Audit Skill — devblog-starter backend

## Proje Bağlamı

Bu repoda auth/security tarafı bilinçli olarak **dev-only kısayollar** içeriyor
(`CLAUDE.md` → Technical Debt bölümünde de not edilmiş): JWT secret hardcoded,
parolalar base64 ile "hash"leniyor, CORS herkese açık. Bu skill'in amacı bunları
tekrar tekrar keşfetmek değil — bilinen zaten kritik bulguları hızlıca teyit edip,
asıl değeri **her endpoint'in flow'una özel, kod değiştikçe ortaya çıkabilecek yeni
riskleri** yakalamakta bulmak. Denetim sırasında aşağıdaki "bilinen durum" listesini
güncel kodla karşılaştırarak doğrula — biri düzeltilmişse tekrar bulgu olarak
raporlama, hâlâ duruyorsa raporla.

Backend minimal API stilinde (`Program.cs`'de `PostsEndpoint`, `CommentsEndpoint`,
`AuthEndpoint` register ediliyor). `PostsEndpoint`/`PostService`/`PostRepository`
Endpoint→Service→Repository katmanına geçmiş durumda; `CommentsEndpoint` ve
`AuthEndpoint` hâlâ `AppDbContext`'i doğrudan enjekte ediyor (bkz. CLAUDE.md
Technical Debt) — katman eksikliği kendi başına güvenlik bulgusu değil ama
validation/yetkilendirme mantığının nereye ekleneceğini etkiler, önerilerde bunu
göz önünde bulundur.

## Denetim Yöntemi

Her endpoint için şu akışı izle: **giriş noktası → input alınışı → yetkilendirme →
veri erişimi → çıkış/response**. Her adımda hangi OWASP Top 10 (2025) kategorisinin
ilgili olduğunu belirt: A01 Broken Access Control (artık SSRF'i de kapsıyor), A02
Security Misconfiguration, A03 Software Supply Chain Failures (yeni — eski "Vulnerable
and Outdated Components"un genişletilmiş hali), A04 Cryptographic Failures, A05
Injection, A06 Insecure Design, A07 Authentication Failures, A08 Software or Data
Integrity Failures, A09 Security Logging & Alerting Failures, A10 Mishandling of
Exceptional Conditions (yeni). Numaralandırma 2021'e göre değişti (ör. Security
Misconfiguration A05→A02, Cryptographic Failures A02→A04, Injection A03→A05, Insecure
Design A04→A06) — eski raporlarla veya hafızandaki 2021 numaralarıyla karıştırma.
Statik analiz yap — kod çalıştırma/exploit deneme yok, sadece kod okuma ve rapor
üretme.

### `POST /auth/login` — `AuthEndpoint.cs`

- **Parola saklama**: `Convert.ToBase64String` bir hash değil, tersine çevrilebilir
  encoding — DB'ye erişen biri tüm parolaları anında okuyabilir. Kritik, A04.
- **JWT secret**: `Program.cs` ve `AuthEndpoint.cs`'de aynı string iki yerde
  hardcoded ve kaynak kontrolünde — repoya erişen herkes geçerli token
  imzalayabilir. Kritik, A04/A02. İkisinin senkron kalması gerekliliği de ayrı bir
  bakım riski.
- **Token validation**: `ValidateIssuer = false, ValidateAudience = false` — tek
  servisli bu uygulamada etkisi düşük ama başka bir servis aynı secret'ı paylaşırsa
  token confusion riski oluşturur; not düş, kritik olarak işaretleme.
- **Brute force / rate limiting**: Endpoint'te herhangi bir deneme sınırı yok,
  `AddRateLimiter` kullanılmıyor — kaba kuvvet saldırısına açık. A07.
- **Kullanıcı adı enumeration**: Başarısız girişte tek tip `Results.Unauthorized()`
  dönüyor (kullanıcı var/yok ayrımı yapmıyor) — bu doğru, düzeltme önerme, sadece
  regresyon kontrolü olarak doğrula.
- **Varsayılan admin kimlik bilgisi**: `DataSeeder.cs`'de `admin/admin` seed
  ediliyor; `Users` tablosu boşsa otomatik oluşuyor ve prod'da da aynı seed kodu
  koşuyor (`Program.cs`'de ortam kontrolü yok). Prod'a bu haliyle taşınırsa bilinen
  varsayılan kimlik bilgisiyle giriş riski. A07/A02.

### `POST /posts` — `PostsEndpoint.cs` + `PostService.cs`

- **Yetkilendirme**: `.RequireAuthorization()` sadece "authenticated mi" kontrolü
  yapıyor; `User.Role` (`Admin`/`Author`) hiçbir policy'de kullanılmıyor — herhangi
  bir authenticated kullanıcı herhangi bir yazar adına olmasa da post oluşturabilir
  (kendi `authorId`'siyle, IDOR değil ama rol ayrımı yok). Rol ayrımının kasıtlı
  olup olmadığını netleştir, otomatik "hata" gibi raporlama — sistem tasarımı
  "her authenticated kullanıcı yazabilir" olabilir.
- **Input validation**: `CreatePostRequest(Title, Content, Slug, Tags)` üzerinde
  uzunluk/boşluk/format kontrolü yok — boş `Title`, aşırı uzun `Content` (DoS/depolama
  riski), keyfi `Slug` formatı (URL'de sorun çıkarabilecek karakterler) kabul
  ediliyor. A06.
- **Slug uniqueness**: `PostService.CreatePostAsync` artık `AnyAsync(p => p.Slug ==
  request.Slug)` ile kontrol ediyor — CLAUDE.md'deki "server-side slug-uniqueness
  check yok" notu **güncel değil**, denetimde bunu tekrar bulgu olarak raporlama;
  CLAUDE.md'nin bu maddesinin güncellenmesi gerektiğini ayrıca belirt.
- **Mass assignment**: Request record'u sadece izin verilen alanları taşıyor
  (`AuthorId`/`Id` client'tan gelmiyor, sunucuda claim'den atanıyor) — güvenli,
  regresyon kontrolü olarak doğrula.
- **Stored content**: `Content` alanı frontend'de `{{ post.content }}` interpolation
  ile basılıyor (Angular auto-escape) — mevcut haliyle stored XSS riski düşük;
  ancak backend hiçbir sanitization/uzunluk sınırı uygulamıyor, frontend tarafında
  `innerHTML`/`bypassSecurityTrustHtml` kullanılan bir değişiklik olursa bu aynı
  veri kritik hale gelir — bunu "gizli/uyuyan risk" olarak Medium severity'de not
  düş.

### `GET /posts`, `GET /posts/{slug}` — `PostsEndpoint.cs`

- **Katman tutarsızlığı**: `GET /posts` `IPostService` üzerinden gidiyor, `GET
  /posts/{slug}` hâlâ `AppDbContext`'i doğrudan enjekte ediyor — güvenlik açısından
  şu an fark yaratmıyor (ikisi de salt-okunur, yetkilendirme gerektirmiyor) ama
  ileride slug endpoint'ine yetkilendirme/filtreleme eklenmesi gerekirse
  tutarsız katmanlama unutulmasına yol açabilir; Info severity.
  Not: `Repositories/PostRepository.cs`'deki LIKE tag filtresi zaten `%`/`_`/`\`
  karakterlerini escape ediyor — SQL/LIKE injection'a karşı doğru şekilde
  korunuyor, tekrar bulgu üretme.
- **Enumeration/veri ifşası**: Slug bulunamazsa `Results.NotFound()` dönüyor,
  bilgi sızdırmıyor — regresyon kontrolü olarak doğrula.
- **`pageSize` sınırı**: `PostService.GetPostsAsync` içinde `Math.Clamp(pageSize,
  1, 100)` var — DoS amaçlı aşırı büyük sayfa isteğine karşı korunuyor, tekrar
  bulgu üretme.

### `POST /posts/{slug}/comments` — `CommentsEndpoint.cs`

- **Yetkilendirme yok**: Endpoint tamamen anonim, `.RequireAuthorization()` yok —
  bu muhtemelen kasıtlı (herkes yorum yapabilsin) ama bu durumda **spam/rate
  limiting hiç yok**, tek bir slug'a sınırsız sayıda yorum eklenebilir. A06/Kaynak
  tüketimi. Kasıtlı bir tasarım kararı mı yoksa gözden kaçmış mı olduğunu belirt.
- **Input validation**: `CreateCommentRequest(AuthorName, Body)` için uzunluk/boşluk
  kontrolü yok — boş `AuthorName`/`Body`, aşırı uzun `Body` kabul ediliyor. A06.
- **Doğrudan `AppDbContext` kullanımı**: Servis/repository katmanı yok
  (CLAUDE.md Technical Debt'te zaten not edilmiş) — validation eklenecekse bunun
  nereye taşınacağını (Service katmanı) öner, endpoint içine gömme.
- **XSS**: `post-detail.component.html`'de `{{ c.body }}`/`{{ c.authorName }}`
  interpolation ile basılıyor, Angular auto-escape uyguluyor — mevcut haliyle
  düşük risk, `POST /posts` bölümündeki aynı "uyuyan risk" notu burada da geçerli.

### `Program.cs` — cross-cutting

- **CORS**: `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()` — herhangi bir
  origin'den, herhangi bir header/method ile istek kabul ediliyor; credential'lı
  istek yapılmıyor olsa bile (JWT header ile taşınıyor, cookie değil) CSRF'e karşı
  ek bir bariyer yok. A02.
- **`UseHttpsRedirection()` çağrılmıyor** — HTTP üzerinden token/parola düz metin
  taşınabilir. A04.
- **Güvenlik header'ları yok**: `X-Content-Type-Options`, `X-Frame-Options`,
  `Content-Security-Policy` gibi header'lar hiç set edilmiyor. A02.
- **Request body boyut sınırı yok**: Kestrel/`MapPost` seviyesinde body boyutu
  sınırlanmamış — büyük `Content`/`Body` alanlarıyla basit bir DoS mümkün. A06.
- **DataSeeder ortam kontrolü yok**: `db.Database.Migrate()` + `DataSeeder.Seed(db)`
  her ortamda (dev/prod ayrımı olmadan) çalışıyor — bkz. yukarıdaki admin/admin
  bulgusu, kök neden burada.
- **Global exception handling yok**: `Program.cs`'de `UseExceptionHandler` veya
  eşdeğeri yok — beklenmeyen istisnalarda response'un ortama göre ne döndüğü
  (stack trace/iç detay sızdırıp sızdırmadığı) denetlenmemiş durumda. A10 (yeni,
  Mishandling of Exceptional Conditions) — denetim sırasında hem dev hem prod
  davranışını ayrı ayrı doğrula, sadece varlık/yokluk değil gerçek response
  içeriğine bak.
- **Bağımlılık/tedarik zinciri**: `backend/src/DevBlog.Api.csproj` ve
  `frontend/devblog-ui/package.json`'daki paket sürümlerinin bilinen CVE'ye sahip
  olup olmadığını kontrol et (ör. `dotnet list package --vulnerable`, `npm audit`).
  A03 (yeni, Software Supply Chain Failures) — bu repoda henüz sistematik olarak
  taranmadı, ilk çalıştırmada bulunanları raporla, sonraki denetimlerde sadece
  değişen/yeni eklenen paketlere odaklan.

## Çıktı Formatı

Her bulgu şu şablonla raporlanır:

```
### [SEVERITY] Kısa başlık — dosya:satır
**OWASP Kategorisi:** A0X - ...
**Bulgu:** Ne yanlış / eksik, kod neyi gösteriyor.
**Etki:** Kim, nasıl istismar eder; hangi veriye/işleve erişir.
**Öneri:** Somut düzeltme (kod değişikliği önerisi olabilir, ama skill kendisi
  kod değiştirmez).
```

Severity ölçeği: **Critical** (uzaktan, kimlik doğrulamasız veri/hesap ele
geçirme), **High** (yetkilendirme atlatma, kimlik bilgisi ifşası), **Medium**
(DoS, bilgi sızıntısı, uyuyan risk), **Low**/**Info** (savunma derinliği, best
practice, tasarım netleştirmesi gereken nokta).

Rapor **endpoint bazında gruplanır** (`AuthEndpoint`, `PostsEndpoint`,
`CommentsEndpoint`, `Program.cs` cross-cutting), en yüksek severity'li bulgu
raporun başında öne çıkarılır. "Zaten korunuyor" olarak doğrulanan noktalar
(SQL injection, pageSize clamp, 404 enumeration vb.) ayrı bir "✅ Kontrol edildi,
bulgu yok" listesinde kısaca belirtilir ki hangi alanların tarandığı belli olsun.
Skill kod değiştirmez, sadece raporlar — düzeltme uygulanması istenirse bunu ayrı
bir onay adımı olarak kullanıcıya sor ve ilgiliyse `migration-guvenlik-kontrolu`
skill'ini (entity/migration değişikliği gerekiyorsa) hatırlat.
