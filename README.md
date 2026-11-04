# Mini DPP – Mini Dijital Ürün Pasaportu

Denim ürünlerine ait CSV verisini okuyan, doğrulayan, geçerli kayıtları SQLite'ta saklayan ve her ürün için
**HTML pasaport**, **schema.org/Product JSON-LD** ve **QR kod (PNG)** üreten küçük bir ASP.NET Core uygulaması.

> ESG Next teknik değerlendirme ödevi. Gerçek proje çıktısı değildir.

## Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows: `winget install Microsoft.DotNet.SDK.8`)
  veya .NET 8 çalışma zamanı kurulu olmak koşuluyla daha yeni bir SDK. Proje `net8.0` hedefler.

Başka bir kurulum gerekmez. SQLite dosyası ve tablolar ilk çalıştırmada otomatik oluşur.

## Çalıştırma

```bash
git clone <repo-url>
cd Odev
dotnet run --project src/MiniDpp.Api
```

Uygulama **http://localhost:5080** adresinde açılır. Veritabanı `src/MiniDpp.Api/minidpp.db` dosyasıdır;
sıfırdan başlamak için bu dosyayı silmeniz yeterli.

## CSV içe aktarma

**Ekran ile:** http://localhost:5080 adresini açın, `samples/urunler_duzeltilmis_test.csv` dosyasını seçip
**Yükle**'ye basın. Kaydedilen ve reddedilen satırlar ile ürün listesi (QR'lar dahil) aynı sayfada görünür.

**API ile:**

```bash
curl -F "file=@samples/urunler_duzeltilmis_test.csv" http://localhost:5080/api/import
```

> Windows PowerShell 5.1'de `curl` bir takma addır; `curl.exe` yazın.

Örnek CSV için beklenen sonuç (başlık = 1. satır):

| CSV satırı | GTIN | Sonuç |
|---|---|---|
| 2 | 08690000000012 | ✅ kaydedilir |
| 3 | 08690000000029 | ✅ kaydedilir |
| 4 | *(boş)* | ❌ `GTIN boş olamaz.` |
| 5 | 08690000000043 | ❌ `Elyaf oranlarının toplamı %100 olmalı (pamuk 95 + elastan 3 = 98).` |
| 6 | 08690000000050 | ✅ kaydedilir |

```json
{
  "totalRows": 5, "validRows": 3, "invalidRows": 2, "created": 3, "updated": 0, "unchanged": 0,
  "imported": [ { "row": 2, "gtin": "08690000000012", "status": "created", "version": 1 }, "..." ],
  "errors": [
    { "row": 4, "gtin": null, "messages": ["GTIN boş olamaz."] },
    { "row": 5, "gtin": "08690000000043", "messages": ["Elyaf oranlarının toplamı %100 olmalı (pamuk 95 + elastan 3 = 98)."] }
  ]
}
```

**Sürümleme demosu (bonus):** Örnek CSV'den sonra `samples/urunler_guncelleme.csv` dosyasını yükleyin.
`…012` güncellenir (karbon 12.4 → 11.9, sürüm 2), `…029` değişmeden kalır, düzeltilmiş `…043` ise yeni kayıt olarak eklenir.

## Örnek URL'ler

| Ne | Adres |
|---|---|
| Yükleme ekranı | http://localhost:5080/ |
| HTML pasaport (tüketici) | http://localhost:5080/dpp/08690000000012 |
| HTML pasaport (yetkili) | http://localhost:5080/dpp/08690000000012?rol=yetkili |
| JSON-LD (tüketici) | http://localhost:5080/dpp/08690000000012.jsonld |
| JSON-LD (yetkili) | http://localhost:5080/dpp/08690000000012.jsonld?rol=yetkili |
| QR kod (PNG) | http://localhost:5080/dpp/08690000000012/qr.png |
| Ürün listesi | http://localhost:5080/api/products |
| Model → batch ilişkisi | http://localhost:5080/api/models |
| Sürüm geçmişi (bonus) | http://localhost:5080/api/products/08690000000012/history?rol=yetkili |

## Mimari ve veri akışı

```
CSV ─► POST /api/import
         │
         ├─ CsvRowReader   başlığı denetler, satırları ham metin olarak okur (GTIN string kalır)
         ├─ RowValidator   her satırı tüm kurallara göre doğrular, tüm hataları birlikte toplar
         └─ ImportService  geçerli satırlar: model bul/oluştur → batch ekle/güncelle (+ sürüm arşivi)
                           hatalı satırlar: {satır no, GTIN, nedenler} olarak rapora eklenir
                           tüm geçerli satırlar tek SaveChanges (tek transaction) ile yazılır

GET /dpp/{gtin}[.jsonld] ─► Batch + Model ─► DppView.From(batch, rol) ─┬─► DppHtmlRenderer
                                              (rol filtresi burada)     └─► JsonLdBuilder
GET /dpp/{gtin}/qr.png   ─► rol içermeyen pasaport URL'si ─► QRCoder ─► PNG
```

```
src/MiniDpp.Api/
  Program.cs            DI, SQLite, endpoint kayıtları
  Domain/               ProductModel, Batch, BatchVersion
  Data/DppDbContext.cs  tablolar, ilişkiler, unique index ve CHECK kısıtları
  Import/               CsvRowReader, RowValidator, Gs1, ImportService
  Dpp/                  ViewerRole, DppView, JsonLdBuilder, DppHtmlRenderer, QrCodePng, DppLinks
  Endpoints/            /api/* ve /dpp/* uç noktaları
  wwwroot/index.html    sade HTML yükleme ekranı (framework yok)
tests/MiniDpp.Tests/    birim + uçtan uca testler
samples/                örnek CSV'ler
```

### Veri modeli

```
product_models (1) ──────< (N) batches (1) ──────< (N) batch_versions
  Id                             Id                        Id, BatchId, Version
  Name  UNIQUE                   Gtin TEXT(14) UNIQUE      ModelName, BatchNo, ... (o anki kopya)
                                 BatchNo                   ValidFromUtc, ArchivedAtUtc
                                 CottonPct, ElastanePct
                                 ProductionPlace
                                 CarbonKgCo2e
                                 Version, CreatedAtUtc, UpdatedAtUtc
                                 ProductModelId  FK
```

### Doğrulama kuralları

| Alan | Kural |
|---|---|
| `gtin` | Boş olamaz, tam 14 ASCII rakam olmalı, GS1 mod-10 kontrol basamağı tutmalı |
| `elyaf_pamuk_yuzde`, `elyaf_elastan_yuzde` | Sayısal, her biri 0–100 aralığında, toplamları tam 100 |
| `model_adi`, `batch_no`, `uretim_yeri` | Boş (veya yalnızca boşluk) olamaz |
| `karbon_kg_co2e` | Sayısal ve ≥ 0 |
| Satır yapısı | Sütun sayısı başlıkla aynı olmalı; aynı dosyada aynı GTIN ikinci kez geçemez |
| Dosya | Boş olamaz; 7 zorunlu sütun başlıkta bulunmalı (yoksa 400 döner, hiçbir satır işlenmez) |

## Tasarım varsayımları ve kararlar

- **GTIN batch düzeyinde tutulur.** CSV'de her satırın kendine ait bir GTIN'i olduğu için pasaport
  (`/dpp/{gtin}`) bir batch kaydına karşılık gelir. Model adı ayrı tabloya normalize edilir; aynı model adına sahip
  satırlar aynı `ProductModel` kaydına bağlanır. GS1 terminolojisinde GTIN aslında ürün tipini, lot ise partiyi
  tanımlar (GS1 Digital Link: `/01/{gtin}/10/{lot}`). Gerçek bir sistemde GTIN modele taşınabilir ve rota
  `/dpp/{gtin}/{lot}` biçimine genişletilebilir; ödevin istediği `/dpp/{gtin}` adresi korunsun diye bu sürümde bu yol seçildi.
- **GTIN her yerde metindir:** CSV string olarak okunur, `TEXT` kolonda saklanır, JSON-LD'de `"gtin14"` string'dir.
  Ek güvence olarak DB'de `length = 14 AND yalnızca rakam` CHECK kısıtı vardır.
- **Tek görünüm modeli, tek rol kararı:** HTML ve JSON-LD aynı `DppView` nesnesinden üretilir. Karbonun
  gösterilip gösterilmeyeceğine yalnızca `ViewerRoles.VisibleCarbon` karar verir. HTML sayfası JSON-LD'yi
  `<script type="application/ld+json">` olarak da gömer; testler gömülü JSON-LD'nin `.jsonld` çıktısıyla birebir aynı olduğunu doğrular.
- **Rol yalnızca demo amaçlıdır:** `?rol=yetkili` (büyük/küçük harf duyarsız) dışındaki her değer veya parametrenin
  olmaması tüketici görünümü demektir. Gerçek yetkilendirme yoktur. Yetkili yanıtlar `Cache-Control: no-store` ile döner.
- **QR kod** her zaman rol parametresi içermeyen (tüketici) pasaport adresine yönlendirir. Adres tabanı isteğin
  geldiği host'tur; telefonla okutmak için `appsettings.json` içinde `Dpp:PublicBaseUrl` ayarlanabilir
  (ör. `http://192.168.1.20:5080`). QR, istek anında üretilir; dosya olarak saklanmaz.
- **Sayılar** kültürden bağımsız (InvariantCulture) okunur; ondalık ayırıcı noktadır (`12.4`). `12,4` reddedilir
  ve hata mesajında ipucu verilir. Sondaki sıfırlar normalize edilir (`98.0` → `98`).
- **Yeniden içe aktarma (upsert):** GTIN yoksa kayıt oluşturulur. Varsa ve içerik değiştiyse önceki hal sürüm
  numarası ve tarihiyle `batch_versions` tablosuna kopyalanır, sürüm 1 artar. İçerik aynıysa hiçbir şey değişmez.
- **Şema yönetimi:** Kurulumu tek komuta indirmek için `EnsureCreated` kullanıldı. Üretimde EF Core migration'ları tercih edilmelidir.
- **CSV/CSRF:** Cookie veya oturum olmadığı için yükleme uç noktasında antiforgery kapalıdır.

## Testler

```bash
dotnet test
```

- **Birim:** GS1 kontrol basamağı, tüm doğrulama kuralları, CSV okuma (satır numarası, BOM, tırnaklı alan, eksik sütun).
- **Uçtan uca** (`WebApplicationFactory` + her test için geçici SQLite): örnek CSV sonucu, model/batch ilişkisi,
  her iki çıktıda rol ayrımı, HTML ile JSON-LD'nin birebir aynı olması, QR PNG, 404 durumları, sürüm geçmişi.

## Kullanılan paketler

- `Microsoft.EntityFrameworkCore.Sqlite`: ORM + SQLite
- `CsvHelper`: CSV ayrıştırma (tırnak, BOM, satır sonu farkları)
- `QRCoder`: QR kodu PNG üretimi (System.Drawing bağımlılığı yok)
