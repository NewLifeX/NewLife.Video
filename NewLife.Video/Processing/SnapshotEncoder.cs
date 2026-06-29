using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Processing;

/// <summary>快照格式编码器。将 VideoFrame 编码为 JPEG/PNG/WebP/BMP 等图像格式</summary>
/// <remarks>
/// JPEG 使用内置纯托管 MjpegCodec 编码（零外部依赖）。PNG/WebP/BMP 通过 FFmpeg 管道编码。
/// 支持将快照保存到文件或返回字节数组。
/// <para>使用示例：
/// <code>
/// var jpegBytes = SnapshotEncoder.EncodeToJpeg(frame, quality: 85);
/// var pngBytes = SnapshotEncoder.EncodeToPng(frame);
/// SnapshotEncoder.SaveSnapshot(frame, "snapshot.webp", SnapshotFormat.WebP);
/// </code>
/// </para>
/// </remarks>
public static class SnapshotEncoder
{
    /// <summary>将 VideoFrame 编码为 JPEG 字节数组（纯托管）</summary>
    /// <param name="frame">视频帧（需为 YUV420P 或 RGB24 格式）</param>
    /// <param name="quality">JPEG 质量 1-100（默认 85）</param>
    /// <returns>JPEG 字节数组</returns>
    public static Byte[] EncodeToJpeg(VideoFrame frame, Int32 quality = 85)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        // 使用内置 MjpegCodec 编码
        var codec = new Codecs.MjpegCodec();
        var vpacket = codec.Encode([frame]);
        if (vpacket?.Data == null) return [];
        return vpacket.Data.ToArray();
    }

    /// <summary>将 VideoFrame 编码为 PNG 字节数组</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>PNG 字节数组</returns>
    public static Byte[] EncodeToPng(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));
        return EncodeViaFFmpeg(frame, "png", "-c:v png -compression_level 6");
    }

    /// <summary>将 VideoFrame 编码为 WebP 字节数组</summary>
    /// <param name="frame">视频帧</param>
    /// <param name="quality">WebP 质量 1-100（默认 80）</param>
    /// <returns>WebP 字节数组</returns>
    public static Byte[] EncodeToWebP(VideoFrame frame, Int32 quality = 80)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));
        return EncodeViaFFmpeg(frame, "webp", $"-c:v libwebp -quality {quality}");
    }

    /// <summary>将 VideoFrame 编码为 BMP 字节数组</summary>
    /// <param name="frame">视频帧（需为 RGB24 或 BGR24 格式）</param>
    /// <returns>BMP 字节数组</returns>
    public static Byte[] EncodeToBmp(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        // BMP 纯托管编码（无外部依赖）
        var rgbData = EnsureRgb24(frame);
        return BuildBmpFile(rgbData, frame.Width, frame.Height);
    }

    /// <summary>保存视频帧为图像文件</summary>
    /// <param name="frame">视频帧</param>
    /// <param name="filePath">输出文件路径</param>
    /// <param name="format">目标格式（默认根据扩展名自动推断）</param>
    /// <param name="quality">质量参数（JPEG/WebP 使用）</param>
    public static void SaveSnapshot(VideoFrame frame, String filePath, SnapshotFormat? format = null, Int32 quality = 85)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));
        if (String.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

        var fmt = format ?? InferFormat(filePath);
        var data = fmt switch
        {
            SnapshotFormat.Jpeg => EncodeToJpeg(frame, quality),
            SnapshotFormat.Png => EncodeToPng(frame),
            SnapshotFormat.WebP => EncodeToWebP(frame, quality),
            SnapshotFormat.Bmp => EncodeToBmp(frame),
            _ => throw new NotSupportedException($"不支持的快照格式：{fmt}"),
        };

        File.WriteAllBytes(filePath, data);
    }

    #region 辅助

    /// <summary>通过 FFmpeg 管道编码单帧为指定图像格式</summary>
    private static Byte[] EncodeViaFFmpeg(VideoFrame frame, String ext, String codecArgs)
    {
        var (pixFmt, rawData) = PrepareRawData(frame);

        var invoker = new FFmpegInvoker();
        if (!invoker.IsAvailable()) return [];

        try
        {
            var args = $"-f rawvideo -pix_fmt {pixFmt} -s {frame.Width}x{frame.Height} -i pipe:0 -frames:v 1 {codecArgs} -f image2 pipe:1";
            return invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>准备 rawvideo 格式数据，返回 (pix_fmt_name, raw_bytes)</summary>
    private static (String pixFmt, Byte[] data) PrepareRawData(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.YUV420P)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return ("yuv420p", data);
        }

        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return ("rgb24", data);
        }

        if (frame.PixelFormat == VideoTypes.BGRA32)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return ("bgra", data);
        }

        // 尝试通过 ffmpeg 转换为 yuv420p
        var pixFmt = frame.PixelFormat switch
        {
            VideoTypes.NV12 => "nv12",
            VideoTypes.NV21 => "nv21",
            _ => "yuv420p",
        };

        var raw = new Byte[frame.Data.Length];
        Array.Copy(frame.Data, raw, frame.Data.Length);

        var invoker = new FFmpegInvoker();
        if (!invoker.IsAvailable()) return ("yuv420p", raw);

        try
        {
            var args = $"-f rawvideo -pix_fmt {pixFmt} -s {frame.Width}x{frame.Height} -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";
            var converted = invoker.ExecutePipeAsync(args, raw).GetAwaiter().GetResult();
            return converted != null ? ("yuv420p", converted) : ("yuv420p", raw);
        }
        catch
        {
            return ("yuv420p", raw);
        }
    }

    /// <summary>确保数据为 RGB24 格式（为 BMP 编码准备）</summary>
    private static Byte[] EnsureRgb24(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        // 其他格式通过 ffmpeg 转为 rgb24
        var (_, rawData) = PrepareRawData(frame);
        var invoker2 = new FFmpegInvoker();
        if (!invoker2.IsAvailable()) return rawData;

        try
        {
            var args = $"-f rawvideo -pix_fmt yuv420p -s {frame.Width}x{frame.Height} -i pipe:0 -f rawvideo -pix_fmt rgb24 pipe:1";
            return invoker2.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult() ?? rawData;
        }
        catch
        {
            return rawData;
        }
    }

    /// <summary>构建 BMP 文件（纯托管）</summary>
    private static Byte[] BuildBmpFile(Byte[] rgbData, Int32 width, Int32 height)
    {
        var rowSize = (width * 3 + 3) & ~3; // 4 字节对齐
        var pixelDataSize = rowSize * height;
        var fileSize = 54 + pixelDataSize; // 14(BMP头) + 40(DIB头) + 像素数据

        var bmp = new Byte[fileSize];

        // BMP 文件头 (14 字节)
        bmp[0] = 0x42; bmp[1] = 0x4D;                  // "BM"
        bmp[2] = (Byte)(fileSize & 0xFF);
        bmp[3] = (Byte)((fileSize >> 8) & 0xFF);
        bmp[4] = (Byte)((fileSize >> 16) & 0xFF);
        bmp[5] = (Byte)((fileSize >> 24) & 0xFF);
        // bmp[6..9] = 0 (reserved)
        bmp[10] = 54; // pixel data offset

        // DIB 头 (40 字节)
        bmp[14] = 40; // DIB header size
        bmp[18] = (Byte)(width & 0xFF);
        bmp[19] = (Byte)((width >> 8) & 0xFF);
        bmp[20] = (Byte)((width >> 16) & 0xFF);
        bmp[21] = (Byte)((width >> 24) & 0xFF);
        bmp[22] = (Byte)(height & 0xFF);
        bmp[23] = (Byte)((height >> 8) & 0xFF);
        bmp[24] = (Byte)((height >> 16) & 0xFF);
        bmp[25] = (Byte)((height >> 24) & 0xFF);
        bmp[26] = 1; bmp[27] = 0; // planes = 1
        bmp[28] = 24; bmp[29] = 0; // bits per pixel = 24

        // 像素数据（BMP 是 BGR 顺序且从底部行开始）
        for (var y = 0; y < height; y++)
        {
            var srcRow = y * width * 3;
            var dstRow = (height - 1 - y) * rowSize + 54;

            for (var x = 0; x < width; x++)
            {
                var srcIdx = srcRow + x * 3;
                var dstIdx = dstRow + x * 3;
                bmp[dstIdx] = rgbData[srcIdx + 2];     // B = R
                bmp[dstIdx + 1] = rgbData[srcIdx + 1]; // G = G
                bmp[dstIdx + 2] = rgbData[srcIdx];     // R = B
            }
        }

        return bmp;
    }

    /// <summary>根据文件路径推断快照格式</summary>
    private static SnapshotFormat InferFormat(String filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => SnapshotFormat.Jpeg,
            ".png" => SnapshotFormat.Png,
            ".webp" => SnapshotFormat.WebP,
            ".bmp" => SnapshotFormat.Bmp,
            _ => SnapshotFormat.Jpeg,
        };
    }

    #endregion
}

/// <summary>快照图像格式枚举</summary>
public enum SnapshotFormat
{
    /// <summary>JPEG 格式（纯托管编码）</summary>
    Jpeg,

    /// <summary>PNG 格式（FFmpeg 编码）</summary>
    Png,

    /// <summary>WebP 格式（FFmpeg 编码）</summary>
    WebP,

    /// <summary>BMP 格式（纯托管编码）</summary>
    Bmp,
}
