using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniDpp.Tests;

/// <summary>Örnek CSV ile içe aktarma → HTML / JSON-LD / QR çıktıları ve rol ayrımı.</summary>
public class DppEndToEndTests
{
    private const string SlimGtin = "08690000000012";

    [Fact]
    public async Task Sample_csv_saves_three_rows_and_reports_rows_4_and_5()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();

        var (response, report) = await app.ImportAsync(client, DppAppFactory.SampleCsv);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, report.GetProperty("totalRows").GetInt32());
        Assert.Equal(3, report.GetProperty("created").GetInt32());

        var saved = report.GetProperty("imported").EnumerateArray().Select(i => i.GetProperty("gtin").GetString());
        Assert.Equal(["08690000000012", "08690000000029", "08690000000050"], saved);

        var errors = report.GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal([4, 5], errors.Select(e => e.GetProperty("row").GetInt32()));
        Assert.Contains("GTIN boş olamaz", errors[0].GetProperty("messages")[0].GetString());
        Assert.Contains("toplamı %100", errors[1].GetProperty("messages")[0].GetString());
    }

    [Fact]
    public async Task Models_and_batches_are_stored_in_related_tables()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var models = JsonDocument.Parse(await client.GetStringAsync("/api/models")).RootElement;

        Assert.Equal(3, models.GetArrayLength());
        var slim = models.EnumerateArray().Single(m => m.GetProperty("name").GetString() == "Slim Denim 501");
        var batch = slim.GetProperty("batches").EnumerateArray().Single();
        Assert.Equal(SlimGtin, batch.GetProperty("gtin").GetString());
        Assert.Equal("B2027-01", batch.GetProperty("batchNo").GetString());
    }

    [Fact]
    public async Task Consumer_view_hides_carbon_in_both_html_and_jsonld()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var html = await client.GetStringAsync($"/dpp/{SlimGtin}");
        var jsonLd = JsonDocument.Parse(await client.GetStringAsync($"/dpp/{SlimGtin}.jsonld")).RootElement;

        Assert.Contains("Slim Denim 501", html);
        Assert.DoesNotContain("12.4", html);
        Assert.DoesNotContain("karbon_kg_co2e", html); // gömülü JSON-LD dahil
        Assert.Null(FindProperty(jsonLd, "karbon_kg_co2e"));
    }

    [Theory]
    [InlineData("yetkili")]
    [InlineData("YETKILI")]
    public async Task Authorized_view_shows_carbon_in_both_html_and_jsonld(string rol)
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var html = await client.GetStringAsync($"/dpp/{SlimGtin}?rol={rol}");
        var jsonLd = JsonDocument.Parse(await client.GetStringAsync($"/dpp/{SlimGtin}.jsonld?rol={rol}")).RootElement;

        Assert.Contains("12.4 kg CO₂e", html);
        Assert.Equal(12.4m, FindProperty(jsonLd, "karbon_kg_co2e")!.Value.GetProperty("value").GetDecimal());
    }

    [Fact]
    public async Task Unknown_role_falls_back_to_consumer_view()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var jsonLd = JsonDocument.Parse(await client.GetStringAsync($"/dpp/{SlimGtin}.jsonld?rol=admin")).RootElement;

        Assert.Null(FindProperty(jsonLd, "karbon_kg_co2e"));
    }

    [Fact]
    public async Task JsonLd_is_schema_org_product_with_text_gtin()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var response = await client.GetAsync($"/dpp/{SlimGtin}.jsonld");
        var jsonLd = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("application/ld+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("https://schema.org", jsonLd.GetProperty("@context").GetString());
        Assert.Equal("Product", jsonLd.GetProperty("@type").GetString());
        Assert.Equal(JsonValueKind.String, jsonLd.GetProperty("gtin14").ValueKind);
        Assert.Equal(SlimGtin, jsonLd.GetProperty("gtin14").GetString());
        Assert.Equal("Slim Denim 501", jsonLd.GetProperty("name").GetString());
        Assert.Equal($"{DppAppFactory.PublicBaseUrl}/dpp/{SlimGtin}", jsonLd.GetProperty("@id").GetString());
        Assert.Equal("B2027-01", FindProperty(jsonLd, "batch_no")!.Value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Numbers_round_trip_through_database_unchanged()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var html = await client.GetStringAsync($"/dpp/{SlimGtin}?rol=yetkili");
        var jsonLd = await client.GetStringAsync($"/dpp/{SlimGtin}.jsonld?rol=yetkili");

        Assert.Contains("%98 pamuk, %2 elastan", html);
        Assert.Contains("\"value\": 98,", jsonLd);
        Assert.Contains("\"value\": 12.4,", jsonLd);
        Assert.DoesNotContain("98.0", jsonLd);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?rol=yetkili")]
    public async Task Html_embeds_exactly_the_same_jsonld_as_the_jsonld_endpoint(string query)
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var html = await client.GetStringAsync($"/dpp/{SlimGtin}{query}");
        var jsonLd = await client.GetStringAsync($"/dpp/{SlimGtin}.jsonld{query}");

        const string open = "<script type=\"application/ld+json\">";
        var start = html.IndexOf(open, StringComparison.Ordinal) + open.Length;
        var embedded = html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)];

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(jsonLd), JsonNode.Parse(embedded)));
    }

    [Fact]
    public async Task Qr_code_is_png()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var response = await client.GetAsync($"/dpp/{SlimGtin}/qr.png");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], bytes[..4]);
    }

    [Theory]
    [InlineData("/dpp/08690000000043")]        // geçersiz satır kaydedilmedi
    [InlineData("/dpp/08690000000043.jsonld")]
    [InlineData("/dpp/08690000000043/qr.png")]
    [InlineData("/dpp/8690000000012")]         // baştaki sıfır olmadan eşleşmez
    public async Task Rejected_or_unknown_gtin_returns_404(string url)
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Import_without_file_returns_400()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();

        var response = await client.PostAsync("/api/import", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Csv_with_missing_columns_returns_400_and_saves_nothing()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();

        var (response, body) = await app.ImportAsync(client, "gtin,model_adi\n08690000000012,Slim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("eksik sütun", body.GetProperty("detail").GetString());
        Assert.Equal("[]", await client.GetStringAsync("/api/products"));
    }

    [Fact]
    public async Task Duplicate_gtin_in_same_file_keeps_first_and_reports_second()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        var csv = DppAppFactory.SampleCsv.TrimEnd() + "\n08690000000012,Slim Denim 501,B2027-99,98,2,Denizli,1\n";

        var (_, report) = await app.ImportAsync(client, csv);

        var duplicate = report.GetProperty("errors").EnumerateArray().Single(e => e.GetProperty("row").GetInt32() == 7);
        Assert.Contains("2. satırda zaten kullanıldı", duplicate.GetProperty("messages")[0].GetString());
    }

    internal static JsonElement? FindProperty(JsonElement jsonLd, string propertyId)
    {
        foreach (var property in jsonLd.GetProperty("additionalProperty").EnumerateArray())
        {
            if (property.GetProperty("propertyID").GetString() == propertyId)
            {
                return property;
            }
        }

        return null;
    }
}
