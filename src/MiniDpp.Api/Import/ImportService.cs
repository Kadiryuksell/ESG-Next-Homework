using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Domain;

namespace MiniDpp.Api.Import;

public enum ImportStatus
{
    Created,
    Updated,
    Unchanged,
}

public sealed record ImportedRow(int Row, string Gtin, ImportStatus Status, int Version);

public sealed record RowError(int Row, string? Gtin, IReadOnlyList<string> Messages);

public sealed record ImportReport(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int Created,
    int Updated,
    int Unchanged,
    IReadOnlyList<ImportedRow> Imported,
    IReadOnlyList<RowError> Errors);

/// <summary>
/// CSV içe aktarma akışı: oku → her satırı doğrula → geçerlileri model/batch olarak kaydet.
/// Hatalı satırlar kaydedilmez ama diğer satırların kaydını engellemez.
/// </summary>
public sealed class ImportService(DppDbContext db, TimeProvider clock)
{
    public async Task<ImportReport> ImportAsync(Stream csvStream, CancellationToken ct = default)
    {
        var rows = CsvRowReader.Read(csvStream);
        var now = clock.GetUtcNow().UtcDateTime;

        var imported = new List<ImportedRow>();
        var errors = new List<RowError>();
        var firstRowByGtin = new Dictionary<string, int>();

        foreach (var raw in rows)
        {
            var result = RowValidator.Validate(raw);
            if (!result.IsValid)
            {
                errors.Add(new RowError(raw.RowNumber, NullIfBlank(raw.Gtin), result.Errors));
                continue;
            }

            var row = result.Value!;
            if (firstRowByGtin.TryGetValue(row.Gtin, out var firstRow))
            {
                errors.Add(new RowError(raw.RowNumber, row.Gtin,
                    [$"Bu GTIN dosyada {firstRow}. satırda zaten kullanıldı; aynı dosyada tekrar edemez."]));
                continue;
            }

            firstRowByGtin[row.Gtin] = raw.RowNumber;
            var (status, version) = await UpsertAsync(row, now, ct);
            imported.Add(new ImportedRow(raw.RowNumber, row.Gtin, status, version));
        }

        // Tüm geçerli satırlar tek transaction'da yazılır.
        await db.SaveChangesAsync(ct);

        return new ImportReport(
            TotalRows: rows.Count,
            ValidRows: imported.Count,
            InvalidRows: errors.Count,
            Created: imported.Count(i => i.Status == ImportStatus.Created),
            Updated: imported.Count(i => i.Status == ImportStatus.Updated),
            Unchanged: imported.Count(i => i.Status == ImportStatus.Unchanged),
            Imported: imported,
            Errors: errors);
    }

    /// <summary>
    /// GTIN yoksa yeni batch oluşturur. Varsa ve içerik değiştiyse eski hali
    /// <see cref="BatchVersion"/> olarak arşivler ve sürümü artırır; içerik aynıysa dokunmaz.
    /// </summary>
    private async Task<(ImportStatus Status, int Version)> UpsertAsync(ValidatedRow row, DateTime now, CancellationToken ct)
    {
        var model = await GetOrCreateModelAsync(row.ModelName, ct);
        var batch = await db.Batches
            .Include(b => b.ProductModel)
            .SingleOrDefaultAsync(b => b.Gtin == row.Gtin, ct);

        if (batch is null)
        {
            batch = new Batch { Gtin = row.Gtin, Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
            ApplyRow(batch, row, model);
            db.Batches.Add(batch);
            return (ImportStatus.Created, batch.Version);
        }

        if (HasSameContent(batch, row))
        {
            return (ImportStatus.Unchanged, batch.Version);
        }

        db.BatchVersions.Add(BatchVersion.SnapshotOf(batch, now));
        ApplyRow(batch, row, model);
        batch.Version++;
        batch.UpdatedAtUtc = now;
        return (ImportStatus.Updated, batch.Version);
    }

    private async Task<ProductModel> GetOrCreateModelAsync(string name, CancellationToken ct)
    {
        // Önce bu import sırasında eklenmiş (henüz kaydedilmemiş) modellere bak.
        var model = db.ProductModels.Local.FirstOrDefault(m => m.Name == name)
                    ?? await db.ProductModels.SingleOrDefaultAsync(m => m.Name == name, ct);

        if (model is null)
        {
            model = new ProductModel { Name = name };
            db.ProductModels.Add(model);
        }

        return model;
    }

    private static void ApplyRow(Batch batch, ValidatedRow row, ProductModel model)
    {
        batch.ProductModel = model;
        batch.BatchNo = row.BatchNo;
        batch.CottonPct = row.CottonPct;
        batch.ElastanePct = row.ElastanePct;
        batch.ProductionPlace = row.ProductionPlace;
        batch.CarbonKgCo2e = row.CarbonKgCo2e;
    }

    private static bool HasSameContent(Batch batch, ValidatedRow row) =>
        batch.ProductModel.Name == row.ModelName &&
        batch.BatchNo == row.BatchNo &&
        batch.CottonPct == row.CottonPct &&
        batch.ElastanePct == row.ElastanePct &&
        batch.ProductionPlace == row.ProductionPlace &&
        batch.CarbonKgCo2e == row.CarbonKgCo2e;

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
