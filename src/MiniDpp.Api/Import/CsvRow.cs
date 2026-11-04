namespace MiniDpp.Api.Import;

/// <summary>
/// CSV'den okunan ham satır. Tüm alanlar bilinçli olarak string tutulur:
/// GTIN'deki baştaki sıfırlar korunur ve sayı dönüşümü doğrulama adımında yapılır.
/// </summary>
/// <param name="RowNumber">CSV satır numarası (başlık = 1).</param>
/// <param name="ParseErrors">Satırın yapısal sorunları (sütun sayısı, tırnak hatası vb.).</param>
public sealed record CsvRow(
    int RowNumber,
    string Gtin,
    string ModelName,
    string BatchNo,
    string CottonPct,
    string ElastanePct,
    string ProductionPlace,
    string CarbonKgCo2e,
    IReadOnlyList<string> ParseErrors);

/// <summary>Tüm kurallardan geçmiş, tipleri dönüştürülmüş satır.</summary>
public sealed record ValidatedRow(
    string Gtin,
    string ModelName,
    string BatchNo,
    decimal CottonPct,
    decimal ElastanePct,
    string ProductionPlace,
    decimal CarbonKgCo2e);
