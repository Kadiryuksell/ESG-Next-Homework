using QRCoder;

namespace MiniDpp.Api.Dpp;

public static class QrCodePng
{
    public const string ContentType = "image/png";

    /// <summary>Verilen metni (pasaport URL'si) PNG QR koduna çevirir.</summary>
    public static byte[] Create(string content, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
