namespace MiniDpp.Api.Import;

/// <summary>CSV başlığında beklenen sütun adları.</summary>
public static class CsvColumns
{
    public const string Gtin = "gtin";
    public const string ModelName = "model_adi";
    public const string BatchNo = "batch_no";
    public const string CottonPct = "elyaf_pamuk_yuzde";
    public const string ElastanePct = "elyaf_elastan_yuzde";
    public const string ProductionPlace = "uretim_yeri";
    public const string CarbonKgCo2e = "karbon_kg_co2e";

    public static readonly IReadOnlyList<string> All =
        [Gtin, ModelName, BatchNo, CottonPct, ElastanePct, ProductionPlace, CarbonKgCo2e];
}
