using System.Globalization;

namespace MiniDpp.Api.Import;

public sealed record RowValidationResult(ValidatedRow? Value, IReadOnlyList<string> Errors)
{
    public bool IsValid => Value is not null;
}

/// <summary>
/// Ödevdeki veri doğrulama kuralları. İlk hatada durmaz; satırdaki tüm hataları birlikte döndürür.
/// </summary>
public static class RowValidator
{
    private const NumberStyles NumberFormat =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static RowValidationResult Validate(CsvRow row)
    {
        var errors = new List<string>(row.ParseErrors);

        var gtin = row.Gtin.Trim();
        ValidateGtin(gtin, errors);

        var modelName = RequireText(row.ModelName, CsvColumns.ModelName, "Model adı", errors);
        var batchNo = RequireText(row.BatchNo, CsvColumns.BatchNo, "Batch numarası", errors);
        var place = RequireText(row.ProductionPlace, CsvColumns.ProductionPlace, "Üretim yeri", errors);

        var cotton = ParsePercent(row.CottonPct, CsvColumns.CottonPct, errors);
        var elastane = ParsePercent(row.ElastanePct, CsvColumns.ElastanePct, errors);
        if (cotton is { } c && elastane is { } e && c + e != 100m)
        {
            errors.Add($"Elyaf oranlarının toplamı %100 olmalı (pamuk {Format(c)} + elastan {Format(e)} = {Format(c + e)}).");
        }

        var carbon = ParseNumber(row.CarbonKgCo2e, CsvColumns.CarbonKgCo2e, errors);
        if (carbon < 0)
        {
            errors.Add($"{CsvColumns.CarbonKgCo2e} negatif olamaz (gelen: {Format(carbon.Value)}).");
            carbon = null;
        }

        if (errors.Count > 0)
        {
            return new RowValidationResult(null, errors);
        }

        return new RowValidationResult(
            new ValidatedRow(gtin, modelName!, batchNo!, cotton!.Value, elastane!.Value, place!, carbon!.Value),
            []);
    }

    private static void ValidateGtin(string gtin, List<string> errors)
    {
        if (gtin.Length == 0)
        {
            errors.Add("GTIN boş olamaz.");
        }
        else if (!Gs1.IsFourteenDigits(gtin))
        {
            errors.Add($"GTIN 14 haneli ve yalnızca rakamlardan oluşmalı (gelen: '{gtin}', {gtin.Length} karakter).");
        }
        else if (!Gs1.IsValidGtin14(gtin))
        {
            var expected = Gs1.ComputeCheckDigit(gtin.AsSpan(0, 13));
            errors.Add($"GTIN kontrol basamağı hatalı (beklenen: {expected}, gelen: {gtin[^1]}).");
        }
    }

    private static string? RequireText(string value, string column, string label, List<string> errors)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            errors.Add($"{label} ({column}) boş olamaz.");
            return null;
        }

        return trimmed;
    }

    private static decimal? ParsePercent(string value, string column, List<string> errors)
    {
        var number = ParseNumber(value, column, errors);
        if (number is < 0m or > 100m)
        {
            errors.Add($"{column} 0–100 aralığında olmalı (gelen: {Format(number.Value)}).");
            return null;
        }

        return number;
    }

    private static decimal? ParseNumber(string value, string column, List<string> errors)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            errors.Add($"{column} boş olamaz.");
            return null;
        }

        if (!decimal.TryParse(trimmed, NumberFormat, CultureInfo.InvariantCulture, out var number))
        {
            var hint = trimmed.Contains(',') ? " Ondalık ayırıcı olarak nokta (.) kullanın." : "";
            errors.Add($"{column} sayısal olmalı (gelen: '{trimmed}').{hint}");
            return null;
        }

        return Normalize(number);
    }

    /// <summary>
    /// Sondaki sıfırları atar ("12.40" → 12.4, "98.0" → 98). decimal ölçeği (scale) korunduğu için
    /// aksi halde aynı değer HTML ve JSON-LD'de farklı yazılabilirdi.
    /// </summary>
    private static decimal Normalize(decimal value) =>
        decimal.Parse(value.ToString("G29", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
