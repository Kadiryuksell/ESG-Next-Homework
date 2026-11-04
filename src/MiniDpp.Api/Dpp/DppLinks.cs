namespace MiniDpp.Api.Dpp;

/// <summary>DPP adreslerini tek yerden üretir.</summary>
public sealed class DppLinks(IConfiguration configuration)
{
    /// <summary>
    /// QR kodu ve JSON-LD @id için mutlak adres tabanı. "Dpp:PublicBaseUrl" ayarlıysa o kullanılır
    /// (ör. telefondan okutmak için http://192.168.1.20:5080); değilse isteğin geldiği adres.
    /// </summary>
    public string BaseUrl(HttpRequest request)
    {
        var configured = configuration["Dpp:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(configured)
            ? $"{request.Scheme}://{request.Host}{request.PathBase}"
            : configured.TrimEnd('/');
    }

    /// <summary>Rol içermeyen, kalıcı (tüketici) pasaport adresi.</summary>
    public string PassportAbsolute(HttpRequest request, string gtin) => $"{BaseUrl(request)}{Passport(gtin)}";

    public static string Passport(string gtin, ViewerRole role = ViewerRole.Consumer) => $"/dpp/{gtin}{role.ToQueryString()}";

    public static string JsonLd(string gtin, ViewerRole role = ViewerRole.Consumer) => $"/dpp/{gtin}.jsonld{role.ToQueryString()}";

    public static string Qr(string gtin) => $"/dpp/{gtin}/qr.png";
}
