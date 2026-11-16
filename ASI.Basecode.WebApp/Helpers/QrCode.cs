using QRCoder;

namespace ASI.Basecode.WebApp.Helpers
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
            // viewBox sizing lets CSS scale the code to its container; fixed width/height
            // attributes would crop it whenever the label is narrower than the native size.
            return svg.GetGraphic(pixelsPerModule, "#000000", "#FFFFFF", drawQuietZones: true,
                sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
        }
    }
}
