using System.Globalization;

namespace MiniDpp.Api.Dpp;

/// <summary>HTML ve JSON-LD'nin aynı metni göstermesi için ortak biçimlendirme.</summary>
public static class DppFormat
{
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Material(DppView view) =>
        $"%{Number(view.CottonPct)} pamuk, %{Number(view.ElastanePct)} elastan";

    public static string DateTimeUtc(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
