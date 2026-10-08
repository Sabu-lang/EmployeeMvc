using QRCoder;

namespace EmployeeMvc.Services
{
    public static class QrCodeHelper
    {
        public static string ToPngDataUri(string text)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(5);
            return "data:image/png;base64," + Convert.ToBase64String(png);
        }
    }
}
