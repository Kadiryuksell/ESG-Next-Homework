namespace MiniDpp.Api.Dpp;

/// <summary>Demo amaçlı görünürlük rolü. Gerçek kimlik doğrulama yoktur; rol query string'den gelir.</summary>
public enum ViewerRole
{
    Consumer,
    Authorized,
}

public static class ViewerRoles
{
    public const string QueryKey = "rol";
    public const string AuthorizedValue = "yetkili";

    /// <summary>Yalnızca "?rol=yetkili" yetkili görünüm açar; parametre yoksa veya tanınmıyorsa tüketici.</summary>
    public static ViewerRole Parse(string? value) =>
        string.Equals(value?.Trim(), AuthorizedValue, StringComparison.OrdinalIgnoreCase)
            ? ViewerRole.Authorized
            : ViewerRole.Consumer;

    /// <summary>
    /// Karbon değerinin görünürlük kararı yalnızca burada verilir. HTML, JSON-LD ve sürüm geçmişi
    /// bu metodu kullanır; böylece çıktılar arasında rol tutarsızlığı oluşamaz.
    /// </summary>
    public static decimal? VisibleCarbon(this ViewerRole role, decimal carbonKgCo2e) =>
        role == ViewerRole.Authorized ? carbonKgCo2e : null;

    public static string ToQueryString(this ViewerRole role) =>
        role == ViewerRole.Authorized ? $"?{QueryKey}={AuthorizedValue}" : "";
}
