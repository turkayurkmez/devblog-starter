---
name: seo-audit-skill
description: >
  devblog-starter reposunun frontend'ine (frontend/devblog-ui, Angular 22,
  standalone component'ler, CSR-only) özel SEO denetimi yapar. Bir sayfa/route/
  component eklendiğinde veya değiştiğinde meta tag'leri, başlık hiyerarşisini,
  semantik HTML'i ve Angular'a özgü SEO mekanizmalarını (Title/Meta servisleri,
  canonical, Open Graph, JSON-LD, SSR/prerender durumu) kontrol eder.
  Tetikleyiciler: "SEO kontrolü yap", "bu sayfanın meta bilgilerini incele",
  "bu route SEO açısından uygun mu", "post-detail/post-list/login SEO'sunu
  değerlendir", "yeni bir route/component ekledim, SEO'ya bak".
---

# SEO Audit Skill — devblog-ui (Angular 22)

## Proje Bağlamı

- Uygulama tamamen **client-side render (CSR)** — `frontend/devblog-ui`'da
  `@angular/ssr`, `provideClientHydration` veya `server.ts` yok. Bu, bu reponun
  en yüksek etkili SEO açığıdır: JS çalıştırmayan/render bütçesi kısıtlı
  crawler'lar `<app-root>` boş haldeyken sayfayı görür.
- Routing `app.routes.ts`'de tanımlı, tüm component'ler `loadComponent` ile lazy
  yükleniyor:
  - `''` → `posts`'a redirect
  - `posts` → `PostListComponent`
  - `posts/:slug` → `PostDetailComponent` (asıl indexlenmesi gereken, paylaşılabilir içerik)
  - `login` → `LoginComponent`
- Hiçbir component `@angular/platform-browser`'ın `Title`/`Meta` servislerini
  enjekte etmiyor — `index.html`'deki statik `<title>DevBlog</title>` her route'ta
  aynı kalıyor, `<meta name="description">` hiç yok.
- `frontend/devblog-ui/public/` altında `robots.txt` ve `sitemap.xml` yok.

Bu bölüm kod değiştikçe eskiyebilir — denetim sırasında yukarıdaki varsayımları
(SSR var mı, Title/Meta servisi eklendi mi, yeni route eklendi mi) güncel kodda
tekrar doğrula, körü körüne güvenme.

## Route/Component Bazlı Kontrol Listesi

Her route için aşağıdaki maddeleri ilgili `.component.ts` + `.component.html`
dosya çiftine bakarak değerlendir.

### `AppComponent` (kabuk, tüm route'larda ortak) — `app.component.ts`
- Site geneli `<nav>` semantik etiketle mi kuruluyor (div yığını değil)?
- Aktif route'u işaretleyen `routerLinkActive` var mı (kullanıcı/crawler
  gezinme bağlamını anlayabilsin diye, doğrudan SEO değil ama UX-SEO sınırında)?

### `posts` → `PostListComponent`
- `<h1>` tek ve sayfayı doğru tanımlıyor mu?
- Route'a özgü `<title>` set ediliyor mu (`Title.setTitle(...)`), yoksa hep
  `index.html`'deki jenerik başlık mı kalıyor?
- `<meta name="description">` set ediliyor mu?
- **Sayfalama URL'de mi taşınıyor?** `page` şu an component içi bir alan
  (`page = 1`), route query param'ı değil → sayfa 2+ crawler tarafından
  ulaşılamaz/indexlenemez, tüm sayfalar aynı `/posts` URL'sini paylaşır. Bunu
  her denetimde ayrıca kontrol et; düzelmediyse tekrar bulgu olarak raporla.
- Sayfalama butonları `<button (click)>` yerine `[routerLink]` + query param
  ile `<a>` olarak mı kurulu (crawler tıklama olayını takip edemez, link'i takip
  eder)?

### `posts/:slug` → `PostDetailComponent`
- Bu, reponun en SEO-kritik route'u (paylaşılabilir blog içeriği) — kontrolleri
  buna göre sıkı tut.
- `<article>`/`<section>`/`<h1>`→`<h2>`→`<h3>` hiyerarşisi korunuyor mu
  (mevcut şablon bunu doğru yapıyor, regresyon var mı diye bak)?
- Post yüklendiğinde `Title.setTitle(post.title + ' - DevBlog')` gibi dinamik
  başlık set ediliyor mu?
- `Meta`'ya post özetinden türetilmiş `description` yazılıyor mu?
- `<link rel="canonical">` var mı (slug bazlı URL'lerde tekilleştirme için)?
- Open Graph / Twitter Card meta'ları (`og:title`, `og:description`, `og:image`,
  `og:type=article`) set ediliyor mu? Bunlar yoksa sosyal paylaşımda boş
  önizleme çıkar — blog için önemli.
- JSON-LD (`schema.org/BlogPosting` veya `Article`) enjekte ediliyor mu (rich
  snippet potansiyeli)?
- `ngOnInit` içindeki `subscribe` + `detectChanges` akışı CSR olduğu için içerik
  ilk paint'te DOM'da yok — SSR/prerender yoksa bunu bulgu olarak not et (bkz.
  "Uygulama Geneli" bölümü), component'e özel bir kusur değil ama bu route için
  etkisi en büyük.

### `login` → `LoginComponent`
- `<h1>` var mı (var, regresyon kontrolü)?
- `<meta name="robots" content="noindex, follow">` set ediliyor mu? Login
  sayfaları genelde indexlenmemeli — eksikse bunun bilinçli bir karar mı yoksa
  gözden kaçmış mı olduğunu belirt, otomatik "hata" gibi raporlama.

## Angular 22'ye Özel Kurallar

- **Title/Meta servisleri**: `@angular/platform-browser`'dan `Title` ve `Meta`,
  ilgili route component'inde `inject()` ile alınıp veri geldiğinde
  (`ngOnInit`/subscribe içinde) set edilmeli. Component-level `providers` değil,
  kök seviyede zaten sağlanıyorlar (`BrowserModule`/`bootstrapApplication`
  altında) — sadece inject edip çağırmak yeterli.
- **Standalone component'ler**: Bu repoda NgModule yok, tüm component'ler
  standalone. SEO açısından fark yaratmaz ama meta-güncelleme mantığını ortak
  bir `SeoService` gibi paylaşılan bir yapıya çıkarmak istenirse, bunu
  `providedIn: 'root'` bir Angular service olarak kur, NgModule tabanlı bir
  çözüm önerme.
- **`loadComponent` ile route-level code splitting**: SEO açısından zararsız
  *SSR/prerender ile birlikte kullanıldığında*; CSR-only bir uygulamada lazy
  chunk'lar ilk HTML'de zaten boş olan içeriği daha da geciktirir — SSR yokken
  bunu ayrı bir risk maddesi olarak değerlendirme, mevcut SSR-yok bulgusuna
  dahil et, tekrar etme.
- **`@if`/`@for` yeni control-flow syntax'ı**: `*ngIf`/`*ngFor` yerine
  kullanılması SEO'yu etkilemez, statik analiz sırasında karıştırmamak için not
  düş yeterli.
- **`NgOptimizedImage`**: Şu an hiçbir template'te `<img>` yok. İleride post
  içeriğine görsel eklenirse, `<img>` yerine `NgOptimizedImage` (`ngSrc`)
  kullanılmasını öner (LCP/Core Web Vitals, dolayısıyla SEO sıralaması için).
- **SSR/prerender (`@angular/ssr`, `ng add @angular/ssr`)**: Bu reponun en
  yüksek etkili tek SEO iyileştirmesi. Denetim sırasında `package.json`'da
  `@angular/ssr` olup olmadığını, `app.config.ts`'de
  `provideClientHydration`/server bootstrap olup olmadığını kontrol et. Varsa
  hydration mismatch'lerine (özellikle `ChangeDetectorRef.detectChanges()`
  çağrılarının SSR ile çakışıp çakışmadığına) dikkat çek.
- **Router query param'ları**: Sayfalama/filtreleme gibi durumlar `withRouterConfig`
  veya route `queryParams` ile URL'e yansıtılmalı; component-içi state olarak
  tutulan sayfalama/filtre değerleri crawler'lar için görünmez içerik anlamına
  gelir (bkz. `PostListComponent` bulgusu).

## Uygulama Geneli Kontroller (route'tan bağımsız)

- `frontend/devblog-ui/src/index.html` — statik `<title>` ve `<meta name="description">`
  hâlâ tek/jenerik mi, yoksa route'lar artık kendi başlıklarını mı set ediyor?
- `frontend/devblog-ui/public/robots.txt` var mı?
- `frontend/devblog-ui/public/sitemap.xml` var mı (post slug'ları dinamik
  olduğundan statik dosya yerine backend'den üretilen bir endpoint de kabul
  edilebilir — hangisi varsa onu değerlendir)?
- SSR/prerender durumu (yukarıdaki Angular 22 bölümüne bak).

## Çıktı Formatı

Skill, bulgularını **route/component bazında gruplayarak** raporlar: her route
için ✅/⚠️/❌ işaretli madde listesi, kısa gerekçe ve dosya referansı
(`path/to/file.ts:line`). Ardından "Uygulama Geneli" başlığı altında route'tan
bağımsız bulgular (SSR, robots.txt, sitemap.xml, index.html) ayrı raporlanır.
En yüksek etkili bulgu (genelde SSR/prerender eksikliği veya en çok trafik alan
route'taki meta eksikliği) raporun başında öne çıkarılır. Skill kod değişikliği
yapmaz, yalnızca raporlar — düzeltme uygulanması istenirse bunu ayrı bir onay
adımı olarak kullanıcıya sor.
