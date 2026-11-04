using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Domain;
using MiniDpp.Api.Dpp;

namespace MiniDpp.Api.Endpoints;

public static class DppEndpoints
{
    public static IEndpointRouteBuilder MapDppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dpp").WithTags("DPP");

        // "/{gtin}.jsonld" literal sonek içerdiği için "/{gtin}" rotasından daha önceliklidir.
        group.MapGet("/{gtin}.jsonld", GetJsonLd);
        group.MapGet("/{gtin}/qr.png", GetQrPng);
        group.MapGet("/{gtin}", GetHtml);

        return app;
    }

    private static async Task<IResult> GetHtml(
        string gtin, string? rol, DppDbContext db, DppLinks links, HttpContext http, CancellationToken ct)
    {
        var batch = await FindBatchAsync(db, gtin, ct);
        if (batch is null)
        {
            return Results.Content(DppHtmlRenderer.NotFound(gtin), DppHtmlRenderer.ContentType, Encoding.UTF8, StatusCodes.Status404NotFound);
        }

        var view = BuildView(batch, rol, links, http);
        var html = DppHtmlRenderer.Render(view, JsonLdBuilder.Serialize(view));
        return Results.Content(html, DppHtmlRenderer.ContentType, Encoding.UTF8);
    }

    private static async Task<IResult> GetJsonLd(
        string gtin, string? rol, DppDbContext db, DppLinks links, HttpContext http, CancellationToken ct)
    {
        var batch = await FindBatchAsync(db, gtin, ct);
        if (batch is null)
        {
            return Results.Problem($"GTIN {gtin} için kayıtlı ürün yok.", statusCode: StatusCodes.Status404NotFound);
        }

        var view = BuildView(batch, rol, links, http);
        return Results.Content(JsonLdBuilder.Serialize(view), JsonLdBuilder.ContentType, Encoding.UTF8);
    }

    private static async Task<IResult> GetQrPng(
        string gtin, DppDbContext db, DppLinks links, HttpContext http, CancellationToken ct)
    {
        var exists = await db.Batches.AnyAsync(b => b.Gtin == gtin, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        // QR her zaman rol parametresiz (tüketici) pasaport adresine yönlendirir.
        var png = QrCodePng.Create(links.PassportAbsolute(http.Request, gtin));
        return Results.File(png, QrCodePng.ContentType, fileDownloadName: null);
    }

    private static Task<Batch?> FindBatchAsync(DppDbContext db, string gtin, CancellationToken ct) =>
        db.Batches
            .AsNoTracking()
            .Include(b => b.ProductModel)
            .SingleOrDefaultAsync(b => b.Gtin == gtin, ct);

    private static DppView BuildView(Batch batch, string? rol, DppLinks links, HttpContext http)
    {
        var role = ViewerRoles.Parse(rol);
        if (role == ViewerRole.Authorized)
        {
            // Yetkili içerik ara katmanlarda/tarayıcıda önbelleğe alınmasın.
            http.Response.Headers.CacheControl = "no-store";
        }

        return DppView.From(batch, role, links.PassportAbsolute(http.Request, batch.Gtin));
    }
}
