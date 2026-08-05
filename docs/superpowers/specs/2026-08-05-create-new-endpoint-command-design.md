# `/create-new-endpoint` Komutu — Tasarım

## Amaç

devblog-starter reposuna yeni bir backend endpoint eklerken tutarlı, CLAUDE.md'deki hedef mimariye (Endpoint → Service → Repository) uygun bir başlangıç planı üretmek. Kullanıcı HTTP method ve route verir; komut route'un anlamlı olup olmadığına göre ya doğrudan adım adım bir uygulama planı üretir, ya da eksik bilgiyi netleştirmek için soru sorar.

## Konum ve Argümanlar

- Dosya: `.claude/commands/create-new-endpoint.md` (proje seviyesi, repoya commit edilir — bu tercih edildi çünkü komut doğrudan CLAUDE.md'deki repo-özel mimari kararlara referans verir).
- Argümanlar: `$1` = HTTP method (GET/POST/PUT/PATCH/DELETE), `$2` = route (örn. `/posts/{slug}/comments`).
- Kullanım örneği: `/create-new-endpoint POST /posts/{slug}/comments`

## Argüman Doğrulama

- `$1` veya `$2` eksikse, komut eksik olanı sorup bekler (plana veya route analizine geçmez).
- `$1` bilinen bir HTTP method değilse (GET/POST/PUT/PATCH/DELETE dışında), kullanıcıya doğru method'u sorar.

## Route Anlamlılık Testi

Route şu şekilde değerlendirilir:

1. Route, `{param}` segmentleri ayıklanarak statik segmentlere bölünür.
2. Kalan ilk statik segment bir kaynak/varlık adına eşlenmeye çalışılır:
   - Bilinen varlıklarla (`Post`, `Comment`, `User` — `backend/src/DevBlog.Api/Models`) eşleşiyor mu?
   - Eşleşmiyorsa, segment makul bir çoğul/isim biçiminde mi (yeni varlık adayı olarak kabul edilebilir mi)?
3. Method + route şekli bilinen REST kalıplarından birine uyuyor mu:
   - `GET /resource` → liste
   - `GET /resource/{id}` → tekil getir
   - `POST /resource` → oluştur
   - `PUT` veya `PATCH /resource/{id}` → güncelle
   - `DELETE /resource/{id}` → sil
   - `GET` veya `POST /resource/{id}/subresource` → alt-kaynak (nested) aksiyonu

**Anlamlı** kabul edilme koşulu: kaynak segmenti çözümlenebiliyor VE method+route kalıbı yukarıdaki listeden birine uyuyor.

**Anlamlı değil** kabul edilme durumları (örnekler): fiil-biçimli route'lar (`/do-thing`, `/process`), kısaltılmış/belirsiz segmentler, method+route kombinasyonu yukarıdaki kalıplara uymuyor.

Anlamlı değilse, komut tek tek (bir mesajda bir soru) şu tür sorularla ilerler:
- "Bu route hangi varlık/kaynak ile ilgili?"
- "Bu endpoint ne yapıyor (liste/oluştur/güncelle/sil/özel aksiyon)?"
- "Auth gerekiyor mu?"
- "Request body'de hangi alanlar olacak?"

Yeterli bilgi toplandıktan sonra aynı plan üretim adımına (aşağıda) geçilir.

## Plan Üretimi

Route anlamlı bulunduğunda (veya sorular sonrası yeterli bilgi toplandığında), komut **kod yazmadan**, yalnızca somut bir Markdown adım listesi üretir. Plan CLAUDE.md'deki hedef mimariyi (Endpoint → Service → Repository) esas alır ve yalnızca o endpoint için gerekli olan adımları içerir (var olmayan/gereksiz adımlar atlanır):

1. **Model/Entity** — varlık zaten varsa atla; yoksa yeni model dosyası + `AppDbContext`'te `DbSet` + `OnModelCreating` ilişki config. Yeni migration gerekiyorsa `migration-guvenlik-kontrolu` skill'inin kullanılması gerektiği not edilir.
2. **Repository** — `I<Entity>Repository : IRepository<T>` arayüzü (Interface Segregation — sadece bu endpoint'in service'inin ihtiyaç duyduğu metodlar) + `<Entity>Repository` implementasyonu. Varlığın repository'si zaten varsa, sadece eksik metod eklenir.
3. **Service** — `I<Entity>Service` arayüzü + implementasyonu; iş mantığı burada, `DbContext`'e değil repository'ye bağımlı.
4. **Endpoint** — `<Entity>Endpoint.Map()` içinde route handler (statik sınıf yoksa yeni oluşturulur); `Program.cs`'e DI kaydı ve `Map` çağrısı; method POST/PUT/PATCH/DELETE ise `.RequireAuthorization()` uygulanıp uygulanmayacağı belirtilir (emin değilse bu ayrıca sorulur).
5. **Request/Response sözleşmeleri** — DTO adlandırması repo konvansiyonuna uygun (örn. `Create<Entity>Request`).
6. **Test notu** — CLAUDE.md Technical Debt maddesine referansla, xUnit test projesi yoksa kurulması/genişletilmesi önerisi.
7. **Frontend notu** (yalnızca ilgiliyse) — `frontend/devblog-ui` tarafında karşılık gelen bir servis metodu gerekip gerekmediği not edilir.

## Kapsam Dışı

- Komut kod yazmaz, dosya oluşturmaz/değiştirmez — yalnızca plan (ve gerekiyorsa sorular) üretir.
- Var olan endpoint'lerin (Posts/Comments/Auth) toptan yeniden yazımı bu komutun kapsamında değildir.
