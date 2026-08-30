using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace kEyLite.Services;

/// <summary>从 WPF 位图中解码 QR 码。</summary>
public static class QrService
{
    public static string? DecodeFile(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return Decode(bmp);
    }

    public static string? Decode(BitmapSource source)
    {
        // 大图先缩小，避免 TryHarder 解码过慢
        double scale = Math.Min(1.0, 1400.0 / Math.Max(source.PixelWidth, source.PixelHeight));
        if (scale < 1.0)
        {
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }

        // 先铺白底，处理带透明通道的图片
        var flattened = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight));
            dc.DrawImage(source, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }
        flattened.Render(visual);

        var converted = new FormatConvertedBitmap(flattened, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var luminance = new RGBLuminanceSource(
            pixels, converted.PixelWidth, converted.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            },
        };

        return reader.Decode(luminance)?.Text;
    }
}
