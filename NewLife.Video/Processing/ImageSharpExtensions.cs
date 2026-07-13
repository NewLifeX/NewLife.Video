using NewLife.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NewLife.Video.Processing;

/// <summary>VideoFrame ↔ ImageSharp 互操作扩展方法</summary>
/// <remarks>
/// 桥接 NewLife.Video 的 VideoFrame 与 SixLabors.ImageSharp 的 Image&lt;TPixel&gt;，
/// 利用 ImageSharp 的 12 种图像格式读写能力和丰富的像素处理滤镜。
/// </remarks>
public static class ImageSharpExtensions
{
    /// <summary>将 VideoFrame 转换为 ImageSharp Image (Rgba32)</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>ImageSharp 图像</returns>
    public static Image<Rgba32> ToImageSharpImage(this VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        var image = new Image<Rgba32>(frame.Width, frame.Height);

        switch (frame.PixelFormat)
        {
            case VideoTypes.RGB24:
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < frame.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        var srcOffset = y * frame.Width * 3;
                        for (var x = 0; x < frame.Width; x++)
                        {
                            var si = srcOffset + x * 3;
                            row[x] = new Rgba32(frame.Data[si], frame.Data[si + 1], frame.Data[si + 2], 255);
                        }
                    }
                });
                break;

            case VideoTypes.BGRA32:
                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < frame.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        var srcOffset = y * frame.Width * 4;
                        for (var x = 0; x < frame.Width; x++)
                        {
                            var si = srcOffset + x * 4;
                            row[x] = new Rgba32(frame.Data[si + 2], frame.Data[si + 1], frame.Data[si], frame.Data[si + 3]);
                        }
                    }
                });
                break;

            default:
                // 其他格式通过 FFmpeg 转为 RGB24 再转换
                return ConvertViaFFmpeg(frame);
        }

        return image;
    }

    /// <summary>将 ImageSharp Image 转换为 VideoFrame (RGB24)</summary>
    /// <param name="image">ImageSharp 图像</param>
    /// <returns>VideoFrame</returns>
    public static VideoFrame ToVideoFrame(this Image<Rgba32> image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));

        var data = new Byte[image.Width * image.Height * 3];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < image.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dstOffset = y * image.Width * 3;
                for (var x = 0; x < image.Width; x++)
                {
                    var di = dstOffset + x * 3;
                    data[di] = row[x].R;
                    data[di + 1] = row[x].G;
                    data[di + 2] = row[x].B;
                }
            }
        });

        return new VideoFrame
        {
            Data = data,
            Width = image.Width,
            Height = image.Height,
            PixelFormat = VideoTypes.RGB24,
        };
    }

    /// <summary>通过 ImageSharp 将 VideoFrame 保存为指定格式的图像文件</summary>
    public static void SaveAsImageSharp(this VideoFrame frame, String filePath)
    {
        using var image = frame.ToImageSharpImage();
        image.Save(filePath);
    }

    private static Image<Rgba32> ConvertViaFFmpeg(VideoFrame frame)
    {
        var encoder = new Codecs.MjpegCodec();
        var packet = encoder.Encode([frame]);
        if (packet?.Data != null)
        {
            var data = packet.Data.TryGetArray(out var seg) && seg.Offset == 0
                ? seg.Array : packet.Data.ToArray();
            return Image.Load<Rgba32>(data);
        }
        throw new InvalidOperationException("无法转换帧格式");
    }
}
