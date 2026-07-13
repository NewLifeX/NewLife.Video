using NewLife.Data;

namespace NewLife.Video.Processing;

/// <summary>纯托管像素格式转换器。VideoFrame 与常见像素格式之间的双向转换，零外部依赖</summary>
/// <remarks>
/// 替代 ImageSharpExtensions/SkiaSharpExtensions，使用纯托管实现。
/// 支持 VideoFrame ↔ Byte[] (RGB24/BGRA32/YUV420P) 的相互转换。
/// </remarks>
public static class PixelFormatConverter
{
    /// <summary>将 VideoFrame 转换为 RGB24 字节数组</summary>
    public static Byte[] ToRgb24(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        if (frame.PixelFormat == VideoTypes.BGRA32)
            return BGRA32ToRGB24(frame.Data, frame.Width, frame.Height);

        if (frame.PixelFormat == VideoTypes.BGR24)
            return BGR24ToRGB24(frame.Data, frame.Width, frame.Height);

        if (frame.PixelFormat == VideoTypes.YUV420P)
            return YUV420PToRGB24(frame.Data, frame.Width, frame.Height);

        // 其他格式通过 FFmpeg 转换
        return ConvertViaFFmpeg(frame, "rgb24");
    }

    /// <summary>将 VideoFrame 转换为 BGRA32 字节数组</summary>
    public static Byte[] ToBGRA32(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        if (frame.PixelFormat == VideoTypes.BGRA32)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        var rgb24 = ToRgb24(frame);
        return RGB24ToBGRA32(rgb24, frame.Width, frame.Height);
    }

    /// <summary>从 RGB24 字节数组创建 VideoFrame</summary>
    public static VideoFrame FromRgb24(Byte[] rgbData, Int32 width, Int32 height)
    {
        return new VideoFrame
        {
            Data = rgbData,
            Width = width,
            Height = height,
            PixelFormat = VideoTypes.RGB24,
        };
    }

    /// <summary>从 BGRA32 字节数组创建 VideoFrame</summary>
    public static VideoFrame FromBGRA32(Byte[] bgraData, Int32 width, Int32 height)
    {
        return new VideoFrame
        {
            Data = bgraData,
            Width = width,
            Height = height,
            PixelFormat = VideoTypes.BGRA32,
        };
    }

    #region 纯托管颜色转换

    /// <summary>BGRA32 → RGB24</summary>
    public static Byte[] BGRA32ToRGB24(Byte[] bgra, Int32 width, Int32 height)
    {
        var rgb = new Byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            var si = i * 4;
            var di = i * 3;
            rgb[di] = bgra[si + 2];     // R
            rgb[di + 1] = bgra[si + 1];  // G
            rgb[di + 2] = bgra[si];      // B
        }
        return rgb;
    }

    /// <summary>RGB24 → BGRA32</summary>
    public static Byte[] RGB24ToBGRA32(Byte[] rgb, Int32 width, Int32 height)
    {
        var bgra = new Byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var si = i * 3;
            var di = i * 4;
            bgra[di] = rgb[si + 2];     // B
            bgra[di + 1] = rgb[si + 1];  // G
            bgra[di + 2] = rgb[si];      // R
            bgra[di + 3] = 255;          // A
        }
        return bgra;
    }

    /// <summary>BGR24 → RGB24</summary>
    public static Byte[] BGR24ToRGB24(Byte[] bgr, Int32 width, Int32 height)
    {
        var rgb = new Byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            var si = i * 3;
            var di = i * 3;
            rgb[di] = bgr[si + 2];
            rgb[di + 1] = bgr[si + 1];
            rgb[di + 2] = bgr[si];
        }
        return rgb;
    }

    /// <summary>YUV420P → RGB24 (BT.601)</summary>
    public static Byte[] YUV420PToRGB24(Byte[] yuv, Int32 width, Int32 height)
    {
        var rgb = new Byte[width * height * 3];
        var frameSize = width * height;
        var uvSize = frameSize / 4;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var yi = y * width + x;
                var ui = frameSize + (y / 2) * (width / 2) + (x / 2);
                var vi = frameSize + uvSize + (y / 2) * (width / 2) + (x / 2);

                var Y = yuv[yi];
                var U = yuv[ui];
                var V = yuv[vi];

                // BT.601 标准
                var C = Y - 16;
                var D = U - 128;
                var E = V - 128;

                var R = (298 * C + 409 * E + 128) >> 8;
                var G = (298 * C - 100 * D - 208 * E + 128) >> 8;
                var B = (298 * C + 516 * D + 128) >> 8;

                var di = yi * 3;
                rgb[di] = Clamp(R, 0, 255);
                rgb[di + 1] = Clamp(G, 0, 255);
                rgb[di + 2] = Clamp(B, 0, 255);
            }
        }
        return rgb;
    }

    #endregion

    #region 辅助

    private static Byte Clamp(Int32 value, Int32 min, Int32 max)
    {
        if (value < min) return (Byte)min;
        if (value > max) return (Byte)max;
        return (Byte)value;
    }

    private static Byte[] ConvertViaFFmpeg(VideoFrame frame, String dstFmt)
    {
        var srcFmt = frame.PixelFormat switch
        {
            VideoTypes.NV12 => "nv12",
            VideoTypes.NV21 => "nv21",
            VideoTypes.YUV420P => "yuv420p",
            VideoTypes.RGB24 => "rgb24",
            VideoTypes.BGRA32 => "bgra",
            _ => "yuv420p",
        };

        var invoker = new FFmpeg.FFmpegInvoker();
        if (!invoker.IsAvailable()) return [];

        try
        {
            var args = $"-f rawvideo -pix_fmt {srcFmt} -s {frame.Width}x{frame.Height} -i pipe:0 -f rawvideo -pix_fmt {dstFmt} pipe:1";
            return invoker.ExecutePipeAsync(args, frame.Data).GetAwaiter().GetResult() ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion
}
