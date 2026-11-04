namespace MiniDpp.Api.Domain;

/// <summary>
/// Bir Batch güncellenmeden hemen önceki halinin değişmez kopyası.
/// Model adı da kopyalanır; böylece model sonradan değişse bile geçmiş kayıt doğru kalır.
/// </summary>
public class BatchVersion
{
    public int Id { get; set; }

    public int BatchId { get; set; }

    public Batch Batch { get; set; } = null!;

    public int Version { get; set; }

    public string ModelName { get; set; } = "";

    public string BatchNo { get; set; } = "";

    public decimal CottonPct { get; set; }

    public decimal ElastanePct { get; set; }

    public string ProductionPlace { get; set; } = "";

    public decimal CarbonKgCo2e { get; set; }

    /// <summary>Bu sürümün geçerli olmaya başladığı an (eski kaydın UpdatedAtUtc değeri).</summary>
    public DateTime ValidFromUtc { get; set; }

    /// <summary>Bu sürümün yerini yenisine bıraktığı an.</summary>
    public DateTime ArchivedAtUtc { get; set; }

    public static BatchVersion SnapshotOf(Batch batch, DateTime archivedAtUtc) => new()
    {
        BatchId = batch.Id,
        Version = batch.Version,
        ModelName = batch.ProductModel.Name,
        BatchNo = batch.BatchNo,
        CottonPct = batch.CottonPct,
        ElastanePct = batch.ElastanePct,
        ProductionPlace = batch.ProductionPlace,
        CarbonKgCo2e = batch.CarbonKgCo2e,
        ValidFromUtc = batch.UpdatedAtUtc,
        ArchivedAtUtc = archivedAtUtc,
    };
}
