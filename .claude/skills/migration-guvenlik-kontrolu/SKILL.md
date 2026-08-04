---
name: migration-guvenlik-kontrolu
description: >
  devblog-starter reposuna özel: EF Core migration'ları (backend/src/DevBlog.Api) oluşturmadan
  önce ve oluşturulan migration dosyasını uygulamadan önce güvenlik kontrolü yapar. Kullanıcı
  "migration ekle", "yeni migration oluştur", "entity'ye alan/kolon ekle", "Post/Comment/User'a
  şu özelliği ekle", "dotnet ef migrations add", "database update" gibi bir istekte bulunduğunda
  MUTLAKA bu skill'i kullan. Yeni eklenen non-nullable kolonlar için mevcut kayıtları bozmayacak
  bir varsayılan değer/nullability stratejisi belirler; DropColumn, DropTable, RenameColumn,
  daraltıcı AlterColumn, nullable→NOT NULL geçişi, yeni unique/foreign key kısıtı gibi veri
  kaybına ya da migration'ın patlamasına yol açabilecek riskli işlemler tespit edildiğinde
  `dotnet ef database update` çalıştırılmadan önce kullanıcıdan açık onay ister — otomatik
  olarak ilerlemez.
---

# Migration Güvenlik Kontrolü

Bu repoda migration'lar `Program.cs` içindeki `db.Database.Migrate()` ile **uygulama her
başladığında otomatik uygulanıyor** — hatalı bir migration'ı fark etmeden prod'a taşımak,
diğer projelerdeki gibi "önce staging'de dene" gibi bir güvenlik ağına sahip değil. Ayrıca
`DataSeeder.Seed(db)` `Users` tablosu boşsa örnek kullanıcı/post/yorum ekliyor, yani geliştirme
veritabanında (`devblog.db`) genellikle **gerçek satırlar zaten var** — "boş tabloya migration"
varsayımıyla hareket etmeyin.

Bu skill iki ayrı anda devreye girer: **(1) migration oluşturulmadan önce** entity
değişikliğini analiz eder, **(2) migration dosyası oluşturulduktan sonra**, uygulanmadan
önce son bir güvenlik taraması yapar.

## Aşama 1 — Migration oluşturulmadan önce: entity değişikliğini analiz et

Kullanıcı bir entity'ye (`Models/Post.cs`, `Models/Comment.cs`, `Models/User.cs` ya da yeni
bir entity) alan eklemek istediğinde, `dotnet ef migrations add` çalıştırmadan önce her yeni
property için şunu sorun: **mevcut satırlar bu kolonu nasıl dolduracak?**

1. **Property nullable mı?** (`string?`, `int?`, referans tipi varsayılan `null` olabiliyorsa)
   EF Core kolonu nullable oluşturur, mevcut satırlar `NULL` alır — ek işlem gerekmez, güvenlidir.

2. **Property non-nullable mı?** (`int`, `bool`, `DateTime`, ya da bu repodaki convention'a
   uygun `string Title { get; set; } = "";` gibi C#'ta non-null ama DB'de de NOT NULL
   olması beklenen bir alan) Bu durumda EF Core, mevcut satırlar için bir değer bulmak zorunda
   kalır. Körü körüne "int ise 0, string ise boş string" gibi mekanik bir varsayılan
   uydurmayın — önce anlamlı bir varsayılanın olup olmadığına bakın:
   - **Anlamlı, güvenli bir varsayılan varsa** (ör. yeni bir `bool IsPublished` alanı için
     mevcut tüm post'ların zaten yayında olduğu varsayımı makulse `true`; yeni bir
     `int ViewCount` için `0`), bunu Fluent API ile `HasDefaultValue(...)` olarak modelde
     belirtin ki migration `AddColumn` çağrısına `defaultValue:` gömülsün.
   - **Anlamlı bir varsayılan yoksa** (ör. bir `User`'a eklenen `string Bio` gibi kullanıcıya
     özgü bir alan, ya da başka bir entity'ye zorunlu foreign key), **tahmin etmeyin** —
     kullanıcıya sorun: kolonu nullable mı bırakalım, yoksa belirli bir backfill değeri mi
     (ör. mevcut kullanıcılar için boş string, ya da elle girilecek bir migration script'i)
     istiyor. Yanlış bir varsayılanı sessizce seçmek, veriyi teknik olarak "kurtarır" ama
     anlamsal olarak bozar — bu, drop kadar görünür olmayan ama aynı derecede riskli bir
     hata sınıfıdır.
   - **Zorunlu bir foreign key ekleniyorsa** (ör. `Post`'a yeni bir `int CategoryId`),
     mevcut post'ların hangi kategoriye düşeceği belirsizdir; bu neredeyse her zaman ya
     nullable FK ile başlamayı ya da önce bir "varsayılan kategori" satırı oluşturup ona
     backfill etmeyi gerektirir — bunu kullanıcıyla netleştirmeden migration'ı oluşturmayın.

3. Karar netleştikten sonra `dotnet ef migrations add <Name> --project backend/src/DevBlog.Api/DevBlog.Api.csproj`
   komutunu çalıştırın (ya da kullanıcıdan çalıştırmasını isteyin).

## Aşama 2 — Migration oluşturulduktan sonra: `Up()` metodunu tara

Migration dosyası oluştuğunda (`backend/src/DevBlog.Api/Migrations/*.cs`), `dotnet ef
database update` çalıştırmadan (ya da uygulamayı yeniden başlatıp otomatik migration'ın
tetiklenmesine izin vermeden) **önce** `Up()` metodunu satır satır okuyun ve aşağıdaki
işlemlerden biri varsa **durun ve kullanıcıdan açık onay isteyin** — hiçbirini sessizce
uygulamayın:

| İşlem | Neden riskli |
|---|---|
| `DropColumn` | Kolondaki veri geri dönüşsüz silinir. |
| `DropTable` | Tablodaki tüm veri geri dönüşsüz silinir. |
| `RenameColumn` / `RenameTable` | Veri kaybolmaz ama eski isme referans veren kod (endpoint, sorgu) sessizce kırılabilir. |
| `AlterColumn` — nullable'dan NOT NULL'a geçiş, `defaultValueSql`/`defaultValue` **olmadan** | Mevcut satırlarda o kolon `NULL` ise migration SQLite'ta hata verip yarım kalabilir. |
| `AlterColumn` — daraltıcı tip değişikliği (ör. `TEXT`→`INTEGER`, uzunluk kısaltma) | Mevcut veri yeni tipe/uzunluğa sığmayabilir, sessiz veri kaybı ya da hata riski. |
| Yeni `AddUniqueConstraint` / unique index | Mevcut satırlar arasında zaten tekrar eden değer varsa migration hata verir; bu repoda örneğin `Post.Slug` zaten unique — benzer bir alan eklenirken mevcut veri kontrol edilmeli. |
| Yeni zorunlu `AddForeignKey` | Mevcut satırlardaki referans değeri hedef tabloda yoksa migration hata verir. |

Her tespit için kullanıcıya şunu aktarın: hangi tablo/kolon, veri kaybı ya da hata riski
tam olarak ne, ve mümkünse daha güvenli bir alternatif (ör. önce nullable ekle + backfill
script'i + sonra NOT NULL yapan ikinci bir migration). [[neden-sonuc-mesaji]] skill'indeki
neden→sonuç anlatım biçimini burada da kullanın: *neden bu riskli* → *onaylanmazsa/olduğu
gibi uygulanırsa sonucu ne olur*.

## SQLite'a özgü not

SQLite, `ALTER TABLE`'ı diğer veritabanları kadar zengin desteklemez; EF Core'un SQLite
sağlayıcısı birçok `AlterColumn`/constraint değişikliğini **tabloyu tamamen yeniden
oluşturarak** (geçici tabloya kopyala → eski tabloyu sil → yeniden adlandır) uygular. Bu,
küçük görünen bir "sütun tipini değiştir" işleminin bile tüm tabloyu etkileyen tek bir
işlem olduğu, dolayısıyla bir NOT NULL/tip uyumsuzluğunun **tüm migration'ı** başarısız
kılabileceği anlamına gelir. Bu yüzden Aşama 1'deki varsayılan değer kontrolü, "küçük"
görünen alan eklemelerinde bile atlanmamalı.

## Onay sonrası

Kullanıcı riskli işlemi onaylarsa, onayı aldığınızı ve hangi işlemi/hangi kapsamda
onayladığını kısaca teyit edip devam edin. Onay gelmeden `dotnet ef database update`
çalıştırmayın ya da uygulamayı bu migration'ı otomatik uygulayacak şekilde başlatmayın.
