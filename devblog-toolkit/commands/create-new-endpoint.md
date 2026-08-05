---
description: Verilen HTTP method ve route icin, CLAUDE.md'deki hedef mimariye (Endpoint->Service->Repository) uygun adim adim bir endpoint uygulama plani uretir; route'un anlami cikarilamiyorsa once soru sorar.
argument-hint: [http-method] [route]
context: fork
agent: backend-specialist
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
