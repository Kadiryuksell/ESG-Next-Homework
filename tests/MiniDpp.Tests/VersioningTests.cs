using System.Text.Json;

namespace MiniDpp.Tests;

/// <summary>Bonus: güncellenen ürünün önceki sürümü sürüm numarası ve tarihiyle saklanır.</summary>
public class VersioningTests
{
    private const string Header = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";

    [Fact]
    public async Task Reimporting_identical_data_does_not_create_a_new_version()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, DppAppFactory.SampleCsv);

        var (_, report) = await app.ImportAsync(client, DppAppFactory.SampleCsv);

        Assert.Equal(3, report.GetProperty("unchanged").GetInt32());
        Assert.Equal(0, report.GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task Changed_row_archives_previous_version()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4");

        var (_, report) = await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,11.9");

        var imported = report.GetProperty("imported")[0];
        Assert.Equal("updated", imported.GetProperty("status").GetString());
        Assert.Equal(2, imported.GetProperty("version").GetInt32());

        var history = JsonDocument.Parse(
            await client.GetStringAsync("/api/products/08690000000012/history?rol=yetkili")).RootElement;
        var versions = history.GetProperty("versions").EnumerateArray().ToList();

        Assert.Equal(2, history.GetProperty("currentVersion").GetInt32());
        Assert.Equal([2, 1], versions.Select(v => v.GetProperty("version").GetInt32()));
        Assert.Equal(11.9m, versions[0].GetProperty("carbonKgCo2e").GetDecimal());
        Assert.Equal(12.4m, versions[1].GetProperty("carbonKgCo2e").GetDecimal());
        Assert.Equal(JsonValueKind.String, versions[1].GetProperty("validToUtc").ValueKind);

        var html = await client.GetStringAsync("/dpp/08690000000012?rol=yetkili");
        Assert.Contains("11.9 kg CO₂e", html);
    }

    [Fact]
    public async Task History_hides_carbon_for_consumers()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4");
        await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,11.9");

        var history = JsonDocument.Parse(await client.GetStringAsync("/api/products/08690000000012/history")).RootElement;

        Assert.All(history.GetProperty("versions").EnumerateArray(),
            v => Assert.Equal(JsonValueKind.Null, v.GetProperty("carbonKgCo2e").ValueKind));
    }

    [Fact]
    public async Task Changing_model_name_moves_batch_to_that_model()
    {
        using var app = new DppAppFactory();
        var client = app.CreateClient();
        await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501,B2027-01,98,2,Denizli,12.4");

        await app.ImportAsync(client, $"{Header}\n08690000000012,Slim Denim 501 V2,B2027-01,98,2,Denizli,12.4");

        var jsonLd = JsonDocument.Parse(await client.GetStringAsync("/dpp/08690000000012.jsonld")).RootElement;
        Assert.Equal("Slim Denim 501 V2", jsonLd.GetProperty("name").GetString());

        var history = JsonDocument.Parse(await client.GetStringAsync("/api/products/08690000000012/history")).RootElement;
        Assert.Equal("Slim Denim 501", history.GetProperty("versions")[1].GetProperty("modelName").GetString());
    }
}
