using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace MiniDpp.Api.Import;

/// <summary>Dosya düzeyinde kullanılamayan CSV (boş dosya, eksik sütun). Hiçbir satır işlenmez.</summary>
public sealed class InvalidCsvException(string message) : Exception(message);

/// <summary>
/// CSV'yi ham <see cref="CsvRow"/> listesine çevirir. Bu aşamada iş kuralı doğrulaması yapılmaz;
/// yalnızca satırın yapısal olarak okunabilir olup olmadığı not edilir.
/// </summary>
public static class CsvRowReader
{
    public static IReadOnlyList<CsvRow> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var rowsWithBadQuotes = new HashSet<int>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null, // eksik alan boş kabul edilir ve doğrulamada raporlanır
            BadDataFound = args => rowsWithBadQuotes.Add(args.Context.Parser!.Row),
        };

        using var csv = new CsvReader(reader, config);

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is not { Length: > 0 } header)
        {
            throw new InvalidCsvException("CSV dosyası boş veya başlık satırı okunamadı.");
        }

        var normalizedHeader = header.Select(h => h.Trim().ToLowerInvariant()).ToHashSet();
        var missing = CsvColumns.All.Where(c => !normalizedHeader.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidCsvException(
                $"CSV başlığında eksik sütun(lar): {string.Join(", ", missing)}. " +
                $"Beklenen başlık: {string.Join(",", CsvColumns.All)}");
        }

        var rows = new List<CsvRow>();
        while (csv.Read())
        {
            var rowNumber = csv.Parser.Row;
            var parseErrors = new List<string>();

            if (csv.Parser.Count != header.Length)
            {
                parseErrors.Add($"Sütun sayısı hatalı: beklenen {header.Length}, bulunan {csv.Parser.Count}.");
            }

            if (rowsWithBadQuotes.Contains(rowNumber))
            {
                parseErrors.Add("Satırda hatalı tırnak (\") kullanımı var.");
            }

            string Field(string name) => csv.GetField(name) ?? "";

            rows.Add(new CsvRow(
                rowNumber,
                Field(CsvColumns.Gtin),
                Field(CsvColumns.ModelName),
                Field(CsvColumns.BatchNo),
                Field(CsvColumns.CottonPct),
                Field(CsvColumns.ElastanePct),
                Field(CsvColumns.ProductionPlace),
                Field(CsvColumns.CarbonKgCo2e),
                parseErrors));
        }

        return rows;
    }
}
