---
name: neden-sonuc-mesaji
description: >
  devblog-starter reposuna özel: commit mesajları ve code review açıklamaları için Türkçe,
  neden-sonuç ilişkisi kuran açıklayıcı metinler üretir. Kullanıcı "commit at", "commit mesajı yaz",
  "bu değişikliği commitle", "code review yap", "bu PR'ı incele", "bu diff'i gözden geçir",
  "değişiklikleri özetle" gibi bir istekte bulunduğunda MUTLAKA bu skill'i kullan — hem git commit
  oluşturma akışında hem de kod/diff inceleme akışında devreye girmeli, kullanıcı doğrudan
  çağırmasa bile. CLAUDE.md'deki mimari kararlara (Endpoint→Service→Repository, Technical Debt
  maddeleri) değinen bir değişiklik söz konusu olduğunda da bu skill'i kullanarak o bağlamı
  mesaja taşı.
---

# Neden-Sonuç Mesajı

Bu skill, bu repodaki değişiklikler için commit mesajı ya da code review açıklaması
yazarken kullanılır. Amaç sadece "ne değişti"yi listelemek değil, **neden** bu değişikliğin
yapıldığını ve **sonucunda** somut olarak neyin farklılaştığını birbirine bağlayan,
okuyanın kararı anlayabileceği bir anlatı kurmaktır. Salt "X eklendi" gibi bir liste,
altı ay sonra bu koda bakan birine hiçbir şey anlatmaz; "neden X eklendi, sonucunda ne
mümkün oldu" anlatısı anlatır.

## Ne zaman hangi format kullanılır

- **Commit mesajı**: `git commit` için mesaj üretilirken (kullanıcı commit istiyor ya da
  siz bir değişikliği commit'lemeye hazırlanıyorsunuz).
- **Code review açıklaması**: bir diff/PR incelenirken, bulunan her önemli noktayı
  değerlendirirken.

İkisi de aynı düşünce biçimini (neden → sonuç) paylaşır, sadece uzunluk ve hitap ettiği
okuyucu farklıdır: commit mesajı kısa ve geleceğe dönük bir kayıt, code review açıklaması
daha ayrıntılı ve şu anki karar için gerekçelendirici.

## Bilgiyi nereden topla

Mesajı yazmadan önce değişikliğin gerçek gerekçesini anlayın — uydurmayın:

1. `git diff` / `git diff --staged` ile fiili kod değişikliğine bakın.
2. Konuşma geçmişinde kullanıcının neden bu değişikliği istediğine dair bir ipucu var mı
   kontrol edin (bir bug, bir istek, bir refactor kararı).
3. CLAUDE.md'nin ilgili bölümlerine bakın — özellikle **Technical Debt** ve
   **Architecture** bölümleri. Değişiklik bilinen bir teknik borcu kapatıyorsa
   (ör. bir endpoint'e Service/Repository katmanı eklemek, JWT secret'ı config'e taşımak,
   xUnit test projesi kurmak) bunu neden kısmında açıkça belirtin — bu, gelecekte "neden
   bu şekilde yapılmış" sorusunu baştan yanıtlar.
4. Gerekçeyi bulamıyorsanız uydurmak yerine kod değişikliğinin kendisinden çıkarılabilen
   en dürüst nedeni yazın (ör. "X endpoint'i eksikti, eklendi") — asılsız bir motivasyon
   uydurmaktansa sade bir tespit daha güvenilirdir.

## Commit mesajı formatı

Bu repodaki mevcut commit geçmişi `tip: kısa özet` düzenini kullanıyor (`feat:`, `fix:`,
`docs:`, `refactor:` gibi). Bu düzeni koruyun, başlığı buna göre yazın; gövdeye neden-sonuç
ilişkisini ekleyin:

```
<tip>: <kısa, emir kipinde özet>

Neden: <bu değişikliğin gerekçesi — hangi sorunu çözüyor ya da hangi karara dayanıyor>
Sonuç: <değişikliğin somut etkisi — davranış/API/kullanıcı deneyimi ne yönde değişti>
```

**Örnek:**

```
refactor: PostsEndpoint'e servis katmanı ekle

Neden: CLAUDE.md'deki Endpoint→Service→Repository kararına rağmen PostsEndpoint
AppDbContext'e doğrudan bağımlıydı; bu da iş mantığını test edilemez ve endpoint'e
kilitli hale getiriyordu.
Sonuç: IPostService/PostService araya girdi, PostsEndpoint artık yalnızca HTTP
sözleşmesiyle ilgileniyor; iş mantığı bağımsız olarak test edilebilir.
```

Küçük, tek amaçlı değişikliklerde (ör. bir typo düzeltmesi, bir bağımlılık güncellemesi)
"Neden/Sonuç" gövdesini zorla uzatmayın — başlık tek başına yeterliyse gövdeyi boş bırakın.
Amaç şablonu doldurmak değil, gerçekten açıklayıcı olduğu yerde açıklamaktır.

## Code review açıklaması formatı

Bir diff ya da PR incelenirken, bulunan her önemli noktayı şu sırayla anlatın:

```
**Neden bu bir sorun/dikkat noktası:** <kod neden riskli, tutarsız ya da CLAUDE.md'deki
bir karara/kısıtlamaya aykırı>
**Sonucunda ne olur:** <bu haliyle bırakılırsa somut etki ne olur — bug, tutarsızlık,
gelecekteki bir işi zorlaştırma>
**Öneri:** <somut, uygulanabilir düzeltme>
```

Övgüye değer bir noktayı belirtirken de aynı mantığı kullanın (kısaca): neden iyi bir
karar olduğunu ve sonucunda neyi kolaylaştırdığını birbirine bağlayın — "iyi" demek
yerine "neden iyi" demek, review'u okuyan kişiye aynı kararı başka yerde de tekrar
etmesi için bir gerekçe verir.

Bulguları önem sırasına göre (en riskli/en etkili önce) sıralayın; her biri için dosya ve
satır referansı verin ki okuyan kişi doğrudan koda gidebilsin.

## Dil ve üslup

- Tüm açıklama metni Türkçe yazılır; kod tanımlayıcıları, dosya yolları, teknik terimler
  (ör. `AppDbContext`, `IPostRepository`, endpoint, migration) orijinal haliyle kalır —
  bunları Türkçeleştirmeye çalışmayın, okunabilirliği bozar.
- Emir kipi ve doğrudan cümleler kullanın ("ekle", "kaldır", "taşı"), pasif ve dolaylı
  anlatımdan kaçının.
- Gövdeyi gereksiz yere uzatmayın; "neden" ve "sonuç" birer cümleyle net şekilde
  kurulabiliyorsa uzatmaya gerek yok.
