using MiniDpp.Api.Domain;

namespace MiniDpp.Api.Dpp;

/// <summary>
/// HTML ve JSON-LD çıktılarının ortak kaynağı. Rol filtresi bu modeli oluştururken uygulanır;
/// renderer'lar veritabanı kaydını değil yalnızca bu modeli görür.
/// </summary>
public sealed record DppView(
    string Gtin,
    string ModelName,
    string BatchNo,
    decimal CottonPct,
    decimal ElastanePct,
    string ProductionPlace,
    decimal? CarbonKgCo2e,
    int Version,
    DateTime UpdatedAtUtc,
    ViewerRole Role,
    string PassportUrl)
{
    public static DppView From(Batch batch, ViewerRole role, string passportUrl) => new(
        batch.Gtin,
        batch.ProductModel.Name,
        batch.BatchNo,
        batch.CottonPct,
        batch.ElastanePct,
        batch.ProductionPlace,
        role.VisibleCarbon(batch.CarbonKgCo2e),
        batch.Version,
        batch.UpdatedAtUtc,
        role,
        passportUrl);
}
