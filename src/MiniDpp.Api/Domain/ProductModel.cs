namespace MiniDpp.Api.Domain;

/// <summary>
/// Ürün modeli (ör. "Slim Denim 501"). Bir modelin birden çok batch/lot kaydı olabilir.
/// </summary>
public class ProductModel
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public List<Batch> Batches { get; set; } = [];
}
