namespace MiniDpp.Api.Domain;

/// <summary>
/// Bir modele ait üretim partisi (batch/lot). CSV'deki her geçerli satır bir Batch'tir ve
/// pasaport (/dpp/{gtin}) bu kayıt üzerinden üretilir.
/// </summary>
public class Batch
{
    public int Id { get; set; }

    /// <summary>GTIN-14; baştaki sıfırları korumak için her zaman metin olarak saklanır.</summary>
    public string Gtin { get; set; } = "";

    public string BatchNo { get; set; } = "";

    public decimal CottonPct { get; set; }

    public decimal ElastanePct { get; set; }

    public string ProductionPlace { get; set; } = "";

    public decimal CarbonKgCo2e { get; set; }

    /// <summary>Kayıt her içerik değişikliğinde 1 artar (ilk kayıt = 1).</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int ProductModelId { get; set; }

    public ProductModel ProductModel { get; set; } = null!;

    /// <summary>Önceki sürümlerin arşivi (bonus: sürüm geçmişi).</summary>
    public List<BatchVersion> History { get; set; } = [];
}
