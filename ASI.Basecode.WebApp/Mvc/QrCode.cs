using QRCoder;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Renders QR codes as inline SVG (cross-platform, no System.Drawing). The SVG is generated
    /// from the module matrix only, so the encoded payload never appears as markup.
    /// </summary>
    public static class QrCode
    {
        public static string ToSvg(string payload, int pixelsPerModule = 6)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload ?? string.Empty, QRCodeGenerator.ECCLevel.M);
            var svg = new SvgQRCode(data);
            return svg.GetGraphic(pixelsPerModule);
        }
    }
}
