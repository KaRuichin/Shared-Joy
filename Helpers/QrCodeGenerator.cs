using QRCoder;

namespace Shared_Joy.Helpers;

/// <summary>
/// QR 二维码生成工具
/// —— 使用 QRCoder 的 PngByteQRCode 输出 PNG byte[] → ImageSource
/// </summary>
public static class QrCodeGenerator
{
    /// <summary>
    /// 根据 URL 生成 QR 码 ImageSource
    /// </summary>
    /// <param name="url">要编码的 URL</param>
    /// <param name="pixelsPerModule">每个模块的像素大小</param>
    /// <returns>可直接绑定到 Image 控件的 ImageSource</returns>
    public static ImageSource GenerateQrCode(string url, int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = qrCode.GetGraphic(pixelsPerModule);

        return ImageSource.FromStream(() => new MemoryStream(pngBytes));
    }
}
