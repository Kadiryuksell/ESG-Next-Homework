using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace MiniDpp.Api.Dpp;

/// <summary>
/// İnsan tarafından okunabilir pasaport sayfası. Gösterilen alanlar JSON-LD ile aynıdır ve
/// aynı JSON-LD sayfaya &lt;script type="application/ld+json"&gt; olarak da gömülür.
/// </summary>
public static class DppHtmlRenderer
{
    public const string ContentType = "text/html; charset=utf-8";

    // Türkçe karakterler olduğu gibi kalır; <, >, &, " gibi HTML'e duyarlı karakterler kaçışlanır.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Render(DppView view, string jsonLd)
    {
        var rows = new StringBuilder();
        AddRow(rows, "GTIN", view.Gtin);
        AddRow(rows, "Model", view.ModelName);
        AddRow(rows, "Batch / lot no", view.BatchNo);
        AddRow(rows, "Elyaf bileşimi", DppFormat.Material(view));
        AddRow(rows, "Üretim yeri", view.ProductionPlace);
        if (view.CarbonKgCo2e is { } carbon)
        {
            AddRow(rows, "Karbon ayak izi", $"{DppFormat.Number(carbon)} kg CO₂e");
        }

        AddRow(rows, "Pasaport sürümü", $"{view.Version} (son güncelleme: {DppFormat.DateTimeUtc(view.UpdatedAtUtc)})");

        var isAuthorized = view.Role == ViewerRole.Authorized;
        var roleLabel = isAuthorized ? "Yetkili" : "Tüketici";
        var jsonLdUrl = DppLinks.JsonLd(view.Gtin, view.Role);

        return $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{E(view.ModelName)}} – Dijital Ürün Pasaportu</title>
              <link rel="alternate" type="application/ld+json" href="{{E(jsonLdUrl)}}">
              <style>{{Styles}}</style>
              <script type="application/ld+json">
            {{jsonLd}}
              </script>
            </head>
            <body>
              <main>
                <p class="eyebrow">Dijital Ürün Pasaportu</p>
                <h1>{{E(view.ModelName)}}</h1>
                <p class="role role-{{(isAuthorized ? "authorized" : "consumer")}}">Görünüm: <strong>{{roleLabel}}</strong></p>
                <table>
            {{rows}}    </table>
                <figure>
                  <img src="{{E(DppLinks.Qr(view.Gtin))}}" alt="Bu pasaportun QR kodu" width="160" height="160">
                  <figcaption>QR kod bu sayfanın tüketici görünümünü açar.</figcaption>
                </figure>
                <nav>
                  <a href="{{E(jsonLdUrl)}}">JSON-LD</a>
                  <a href="/">Ürün listesi</a>
                </nav>
              </main>
            </body>
            </html>
            """;
    }

    public static string NotFound(string gtin) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head><meta charset="utf-8"><title>Pasaport bulunamadı</title><style>{{Styles}}</style></head>
        <body><main>
          <p class="eyebrow">Dijital Ürün Pasaportu</p>
          <h1>Pasaport bulunamadı</h1>
          <p>GTIN <strong>{{E(gtin)}}</strong> için kayıtlı ürün yok.</p>
          <nav><a href="/">Ürün listesi</a></nav>
        </main></body>
        </html>
        """;

    private static void AddRow(StringBuilder sb, string label, string value) =>
        sb.Append("      <tr><th>").Append(E(label)).Append("</th><td>").Append(E(value)).AppendLine("</td></tr>");

    private static string E(string value) => Html.Encode(value);

    private const string Styles = """
        body{font-family:system-ui,-apple-system,"Segoe UI",sans-serif;margin:0;background:#f4f6f5;color:#1d2b28}
        main{max-width:640px;margin:2rem auto;background:#fff;padding:2rem;border-radius:12px;box-shadow:0 1px 4px rgba(0,0,0,.08)}
        .eyebrow{margin:0;color:#1f6f5c;font-size:.8rem;letter-spacing:.08em;text-transform:uppercase;font-weight:600}
        h1{margin:.25rem 0 .5rem}
        .role{display:inline-block;padding:.2rem .6rem;border-radius:999px;font-size:.85rem;margin:0 0 1rem}
        .role-consumer{background:#e8f0fe;color:#1a4fa0}.role-authorized{background:#fdecc8;color:#7a4b00}
        table{width:100%;border-collapse:collapse}
        th,td{text-align:left;padding:.55rem .25rem;border-bottom:1px solid #e3e8e6;vertical-align:top}
        th{width:40%;color:#56635f;font-weight:500}
        figure{margin:1.5rem 0;text-align:center}figcaption{font-size:.8rem;color:#56635f}
        nav{display:flex;gap:1rem;flex-wrap:wrap}a{color:#1f6f5c}
        """;
}
