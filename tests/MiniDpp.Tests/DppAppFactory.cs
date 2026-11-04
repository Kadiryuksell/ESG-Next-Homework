using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace MiniDpp.Tests;

/// <summary>Her test için ayrı, geçici SQLite dosyası kullanan uygulama örneği.</summary>
public sealed class DppAppFactory : WebApplicationFactory<Program>
{
    public const string PublicBaseUrl = "https://dpp.example";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"minidpp-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Dpp"] = $"Data Source={_dbPath}",
            ["Dpp:PublicBaseUrl"] = PublicBaseUrl,
        }));
    }

    public static string SampleCsv => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "urunler_duzeltilmis_test.csv"));

    public async Task<(HttpResponseMessage Response, JsonElement Body)> ImportAsync(HttpClient client, string csv)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "urunler.csv");

        var response = await client.PostAsync("/api/import", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response, body);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Geçici dosya; silinemezse sorun değil.
        }
    }
}
