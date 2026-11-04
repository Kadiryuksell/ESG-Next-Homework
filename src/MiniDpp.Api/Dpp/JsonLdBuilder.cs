using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using MiniDpp.Api.Import;

namespace MiniDpp.Api.Dpp;

/// <summary>schema.org/Product tabanlı JSON-LD üretir.</summary>
public static class JsonLdBuilder
{
    public const string ContentType = "application/ld+json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Türkçe karakterler okunabilir kalır; <, >, & gibi HTML'e duyarlı karakterler kaçışlanır,
        // bu yüzden çıktı HTML içindeki <script> etiketine de güvenle gömülebilir.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static JsonObject Build(DppView view)
    {
        var properties = new JsonArray
        {
            Property(CsvColumns.BatchNo, "Batch / lot numarası", view.BatchNo),
            Property(CsvColumns.CottonPct, "Pamuk oranı", view.CottonPct, "%"),
            Property(CsvColumns.ElastanePct, "Elastan oranı", view.ElastanePct, "%"),
            Property(CsvColumns.ProductionPlace, "Üretim yeri", view.ProductionPlace),
        };

        if (view.CarbonKgCo2e is { } carbon)
        {
            properties.Add(Property(CsvColumns.CarbonKgCo2e, "Karbon ayak izi", carbon, "kg CO2e"));
        }

        properties.Add(Property("dpp_surum", "Pasaport sürümü", view.Version));
        properties.Add(Property("dpp_guncelleme", "Son güncelleme (UTC)",
            view.UpdatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));

        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["@id"] = view.PassportUrl,
            ["url"] = view.PassportUrl,
            ["gtin14"] = view.Gtin,
            ["name"] = view.ModelName,
            ["model"] = new JsonObject
            {
                ["@type"] = "ProductModel",
                ["name"] = view.ModelName,
            },
            ["material"] = DppFormat.Material(view),
            ["additionalProperty"] = properties,
        };
    }

    public static string Serialize(DppView view) => Build(view).ToJsonString(SerializerOptions);

    private static JsonObject Property(string id, string name, JsonNode? value, string? unitText = null)
    {
        var property = new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["propertyID"] = id,
            ["name"] = name,
            ["value"] = value,
        };

        if (unitText is not null)
        {
            property["unitText"] = unitText;
        }

        return property;
    }
}
