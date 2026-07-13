using NewLife.Data;
using SkiaSharp;

namespace NewLife.Video.Processing;

/// <summary>VideoFrame ↔ SkiaSharp 互操作扩展方法</summary>
/// <remarks>
/// 桥接 NewLife.Video 的 VideoFrame 与 SkiaSharp 的 SKBitmap/SKImage，
/// 利用 SkiaSharp 的 AVIF/HEIF/JPEG XL 编解码能力。
/// </remarks>
public static class SkiaSharpExtensions
{
    /// <summary>将 VideoFrame 转换为 SkiaSharp SKBitmap</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>SKBitmap</returns>
    public static SKBitmap ToSKBitmap(this VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        switch (frame.PixelFormat)
        {
            case VideoTypes.BGRA32:
                // 直接复制 BGRA 数据
                Marshal.Copy(frame.Data, 0, bitmap.GetPixels(), frame.Data.Length);
                break;

            case VideoTypes.RGB24:
                // RGB24 → BGRA8888
                unsafe
                {
                    var pixels = (Byte*)bitmap.GetPixels();
                    for (var y = 0; y < frame.Height; y++)
                    {
                        for (var x = 0; x < frame.Width; x++)
                        {
                            var si = (y * frame.Width + x) * 3;
                            var di = (y * frame.Width + x) * 4;
                            pixels[di] = frame.Data[si + 2];     // B
                            pixels[di + 1] = frame.Data[si + 1];  // G
                            pixels[di + 2] = frame.Data[si];      // R
                            pixels[di + 3] = 255;                  // A
                        }
                    }
                }
                break;

            default:
                // 通过 FFmpeg 中转
                return ConvertViaFFmpeg(frame);
        }

        return bitmap;
    }

    /// <summary>将 SKBitmap 转换为 VideoFrame (RGB24)</summary>
    /// <param name="bitmap">SKBitmap</param>
    /// <returns>VideoFrame</returns>
    public static VideoFrame ToVideoFrame(this SKBitmap bitmap)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

        var data = new Byte[bitmap.Width * bitmap.Height * 3];
        unsafe
        {
            var pixels = (Byte*)bitmap.GetPixels();
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var si = (y * bitmap.Width + x) * 4;
                    var di = (y * bitmap.Width + x) * 3;
                    data[di] = pixels[si + 2];     // R
                    data[di + 1] = pixels[si + 1];  // G
                    data[di + 2] = pixels[si];      // B
                }
            }
        }

        return new VideoFrame
        {
            Data = data,
            Width = bitmap.Width,
            Height = bitmap.Height,
            PixelFormat = VideoTypes.RGB24,
        };
    }

    /// <summary>将 VideoFrame 编码为 AVIF 字节数组（通过 SkiaSharp）</summary>
    public static Byte[] EncodeToAvif(this VideoFrame frame, Int32 quality = 80)
    {
        using var bitmap = frame.ToSKBitmap();
        using var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Avif, quality);
        return data?.ToArray() ?? [];
    }

    /// <summary>将 VideoFrame 编码为 HEIF 字节数组（通过 SkiaSharp）</summary>
    public static Byte[] EncodeToHeif(this VideoFrame frame, Int32 quality = 80)
    {
        using var bitmap = frame.ToSKBitmap();
        using var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Heif, quality);
        return data?.ToArray() ?? [];
    }

    /// <summary>将 VideoFrame 编码为 JPEG XL 字节数组（通过 SkiaSharp）</summary>
    public static Byte[] EncodeToJpegXl(this VideoFrame frame, Int32 quality = 80)
    {
        using var bitmap = frame.ToSKBitmap();
        using var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Jpegxl, quality);
        return data?.ToArray() ?? [];
    }

    private static SKBitmap ConvertViaFFmpeg(VideoFrame frame)
    {
        // 通过 MJPEG 编码中转
        var encoder = new Codecs.MjpegCodec();
        var packet = encoder.Encode([frame]);
        if (packet?.Data != null)
        {
            var data = packet.Data.ToArray();
            return SKBitmap.Decode(data);
        }
        throw new InvalidOperationException("无法转换帧格式");
    }
}
