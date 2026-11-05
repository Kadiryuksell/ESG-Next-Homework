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

## Kullanılan paketler

- `Microsoft.EntityFrameworkCore.Sqlite`: ORM + SQLite
- `CsvHelper`: CSV ayrıştırma (tırnak, BOM, satır sonu farkları)
- `QRCoder`: QR kodu PNG üretimi (System.Drawing bağımlılığı yok)

## Proje Çıktısı
<img width="1127" height="818" alt="1" src="https://github.com/user-attachments/assets/1b000320-70d7-46ff-bf9c-9546ca5bc339" />
<img width="1101" height="450" alt="2" src="https://github.com/user-attachments/assets/2798c5a5-7c46-4082-907e-3f3ea30f3641" />
<img width="896" height="841" alt="3" src="https://github.com/user-attachments/assets/58b2df62-5da7-4085-b815-5bdd75427648" />
<img width="925" height="857" alt="4" src="https://github.com/user-attachments/assets/f0a2b70a-36fd-4cb2-86e8-47f0cf801cc2" />




