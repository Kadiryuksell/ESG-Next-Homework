using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Dpp;
using MiniDpp.Api.Endpoints;
using MiniDpp.Api.Import;

var builder = WebApplication.CreateBuilder(args);

// Bağlantı dizesi servis oluşturulurken okunur; testler kendi geçici veritabanını verebilir.
// Göreli dosya yolu proje köküne (content root) göre çözülür; uygulama nereden başlatılırsa başlatılsın aynı DB kullanılır.
builder.Services.AddDbContext<DppDbContext>((sp, options) =>
{
    var connection = new SqliteConnectionStringBuilder(
        sp.GetRequiredService<IConfiguration>().GetConnectionString("Dpp") ?? "Data Source=minidpp.db");

    if (connection.DataSource != ":memory:" && !Path.IsPathRooted(connection.DataSource))
    {
        connection.DataSource = Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, connection.DataSource);
    }

    options.UseSqlite(connection.ToString());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DppLinks>();
builder.Services.AddScoped<ImportService>();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var app = builder.Build();

// Demo kolaylığı: şema ilk açılışta oluşturulur (migration aracı gerekmez).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DppDbContext>().Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapApiEndpoints();
app.MapDppEndpoints();

app.Run();

/// <summary>Entegrasyon testlerinde WebApplicationFactory için.</summary>
public partial class Program;
