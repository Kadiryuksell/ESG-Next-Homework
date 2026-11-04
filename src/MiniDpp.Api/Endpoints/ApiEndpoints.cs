using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Dpp;
using MiniDpp.Api.Import;

namespace MiniDpp.Api.Endpoints;

public sealed record ProductLinks(string Passport, string PassportAuthorized, string JsonLd, string JsonLdAuthorized, string Qr);

public sealed record ProductSummary(string Gtin, string ModelName, string BatchNo, int Version, DateTime UpdatedAtUtc, ProductLinks Links);

public sealed record VersionEntry(
    int Version,
    bool IsCurrent,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    string ModelName,
    string BatchNo,
    decimal CottonPct,
    decimal ElastanePct,
    string ProductionPlace,
    decimal? CarbonKgCo2e);

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("API");

        // Kimlik doğrulama/cookie olmadığı için CSRF riski yok; antiforgery bilinçli olarak kapalı.
        api.MapPost("/import", ImportCsv).DisableAntiforgery();
        api.MapGet("/products", ListProducts);
        api.MapGet("/products/{gtin}/history", GetHistory);
        api.MapGet("/models", ListModels);

        return app;
    }

    private static async Task<IResult> ImportCsv(IFormFile? file, ImportService importer, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Results.Problem("CSV dosyası gönderilmedi. multipart/form-data içinde 'file' alanını kullanın.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var report = await importer.ImportAsync(stream, ct);
            return Results.Ok(report);
        }
        catch (InvalidCsvException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IReadOnlyList<ProductSummary>> ListProducts(DppDbContext db, CancellationToken ct)
    {
        var rows = await db.Batches
            .AsNoTracking()
            .OrderBy(b => b.Gtin)
            .Select(b => new { b.Gtin, ModelName = b.ProductModel.Name, b.BatchNo, b.Version, b.UpdatedAtUtc })
            .ToListAsync(ct);

        return rows.Select(r => new ProductSummary(
                r.Gtin, r.ModelName, r.BatchNo, r.Version, r.UpdatedAtUtc,
                new ProductLinks(
                    DppLinks.Passport(r.Gtin),
                    DppLinks.Passport(r.Gtin, ViewerRole.Authorized),
                    DppLinks.JsonLd(r.Gtin),
                    DppLinks.JsonLd(r.Gtin, ViewerRole.Authorized),
                    DppLinks.Qr(r.Gtin))))
            .ToList();
    }

    /// <summary>Bonus: güncel sürüm + arşivlenmiş önceki sürümler. Karbon değeri burada da role tabidir.</summary>
    private static async Task<IResult> GetHistory(string gtin, string? rol, DppDbContext db, CancellationToken ct)
    {
        var batch = await db.Batches
            .AsNoTracking()
            .Include(b => b.ProductModel)
            .Include(b => b.History)
            .SingleOrDefaultAsync(b => b.Gtin == gtin, ct);

        if (batch is null)
        {
            return Results.Problem($"GTIN {gtin} için kayıtlı ürün yok.", statusCode: StatusCodes.Status404NotFound);
        }

        var role = ViewerRoles.Parse(rol);
        var current = new VersionEntry(
            batch.Version, true, batch.UpdatedAtUtc, null,
            batch.ProductModel.Name, batch.BatchNo, batch.CottonPct, batch.ElastanePct, batch.ProductionPlace,
            role.VisibleCarbon(batch.CarbonKgCo2e));

        var previous = batch.History
            .OrderByDescending(h => h.Version)
            .Select(h => new VersionEntry(
                h.Version, false, h.ValidFromUtc, h.ArchivedAtUtc,
                h.ModelName, h.BatchNo, h.CottonPct, h.ElastanePct, h.ProductionPlace,
                role.VisibleCarbon(h.CarbonKgCo2e)));

        return Results.Ok(new { batch.Gtin, CurrentVersion = batch.Version, Versions = previous.Prepend(current).ToList() });
    }

    /// <summary>Model → batch ilişkisini gösterir.</summary>
    private static async Task<IResult> ListModels(DppDbContext db, CancellationToken ct)
    {
        var models = await db.ProductModels
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                m.Id,
                m.Name,
                Batches = m.Batches.OrderBy(b => b.BatchNo).Select(b => new { b.Gtin, b.BatchNo }).ToList(),
            })
            .ToListAsync(ct);

        return Results.Ok(models);
    }
}
