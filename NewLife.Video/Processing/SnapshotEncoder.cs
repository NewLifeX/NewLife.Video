using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Processing;

/// <summary>快照格式编码器。将 VideoFrame 编码为 JPEG/PNG/WebP/BMP/TIFF/GIF/AVIF 等图像格式</summary>
/// <remarks>
/// JPEG 使用内置纯托管 MjpegCodec 编码（零外部依赖）。PNG/WebP/BMP/TIFF/GIF/AVIF 通过 FFmpeg 管道编码。
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
        return vpacket.Data.TryGetArray(out var seg) && seg.Offset == 0 ? seg.Array : vpacket.Data.ToArray();
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

    /// <summary>将 VideoFrame 编码为 TIFF 字节数组</summary>
    /// <param name="frame">视频帧</param>
    /// <param name="compression">压缩算法（默认 none，可选 none/lzw/zip/jpeg，none 为纯托管编码）</param>
    /// <returns>TIFF 字节数组</returns>
    public static Byte[] EncodeToTiff(VideoFrame frame, String compression = "none")
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        // 无压缩时使用纯托管编码
        if (compression == "none" || compression.IsNullOrEmpty())
        {
            var rgbData = EnsureRgb24(frame);
            return BuildTiffFile(rgbData, frame.Width, frame.Height);
        }

        // LZW/ZIP/JPEG 压缩通过 FFmpeg 管道
        return EncodeViaFFmpeg(frame, "tiff", $"-c:v tiff -compression_algo {compression}");
    }

    /// <summary>将 VideoFrame 编码为 GIF 字节数组（FFmpeg 管道，单帧）</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>GIF 字节数组</returns>
    public static Byte[] EncodeToGif(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));
        return EncodeViaFFmpeg(frame, "gif", "-c:v gif");
    }

    /// <summary>将 VideoFrame 编码为 AVIF 字节数组（FFmpeg 管道，需 libaom-av1）</summary>
    /// <param name="frame">视频帧</param>
    /// <param name="quality">质量 1-100（默认 50，越高越好）</param>
    /// <returns>AVIF 字节数组</returns>
    public static Byte[] EncodeToAvif(VideoFrame frame, Int32 quality = 50)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));
        var crf = 63 - (Int32)(quality / 100.0 * 51); // 映射质量到 CRF 范围（12-63）
        if (crf < 12) crf = 12;
        if (crf > 63) crf = 63;
        return EncodeViaFFmpeg(frame, "avif", $"-c:v libaom-av1 -crf {crf} -still-picture 1 -cpu-used 4");
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
            SnapshotFormat.Tiff => EncodeToTiff(frame),
            SnapshotFormat.Gif => EncodeToGif(frame),
            SnapshotFormat.Avif => EncodeToAvif(frame, quality),
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

    /// <summary>构建 TIFF 文件（纯托管，无压缩 baseline RGB）</summary>
    private static Byte[] BuildTiffFile(Byte[] rgbData, Int32 width, Int32 height)
    {
        // TIFF 结构：8 字节头 + IFD(2+tagCount*12+4) + 像素数据 + BitsPerSample 值
        // Baseline TIFF 4.0，RGB 24bit，无压缩

        var tagCount = 9;
        var ifdSize = 2 + tagCount * 12 + 4; // 2(数量) + 12*9(条目) + 4(下一个IFD偏移)
        var ifdOffset = 8;
        var stripOffset = ifdOffset + ifdSize;
        var rowSize = width * 3;
        var imageSize = rowSize * height;

        // BitsPerSample 值放在像素数据之后
        var valueOffset = stripOffset + imageSize;
        var totalSize = valueOffset + 6; // +6 for 3 SHORT values

        var tiff = new Byte[totalSize];

        // TIFF 头 (8 字节)
        tiff[0] = 0x49; tiff[1] = 0x49; // Intel byte order (little-endian)
        tiff[2] = 42;                    // TIFF magic number
        tiff[3] = 0;
        WriteTiffLong(tiff, 4, ifdOffset);

        // IFD 条目数
        WriteTiffShort(tiff, ifdOffset, (Int16)tagCount);
        var idx = ifdOffset + 2;

        // Tag 列表 (每个 12 字节: 2(tag) + 2(type) + 4(count) + 4(value/offset))
        WriteTiffTag(tiff, ref idx, 256, 4, 1, width);           // ImageWidth
        WriteTiffTag(tiff, ref idx, 257, 4, 1, height);          // ImageLength
        WriteTiffTag(tiff, ref idx, 258, 3, 3, valueOffset);     // BitsPerSample (指向值数组)
        WriteTiffTag(tiff, ref idx, 259, 3, 1, 1);               // Compression (1=无压缩)
        WriteTiffTag(tiff, ref idx, 262, 3, 1, 2);               // PhotometricInterpretation (2=RGB)
        WriteTiffTag(tiff, ref idx, 273, 4, 1, stripOffset);     // StripOffsets
        WriteTiffTag(tiff, ref idx, 277, 3, 1, 3);               // SamplesPerPixel
        WriteTiffTag(tiff, ref idx, 278, 4, 1, height);          // RowsPerStrip
        WriteTiffTag(tiff, ref idx, 279, 4, 1, imageSize);       // StripByteCounts

        // Next IFD offset (0 = 无后续 IFD)
        WriteTiffLong(tiff, idx, 0);

        // 像素数据（RGB 顺序，从上到下）
        Array.Copy(rgbData, 0, tiff, stripOffset, imageSize);

        // BitsPerSample 值 (8, 8, 8)
        WriteTiffShort(tiff, valueOffset, 8);
        WriteTiffShort(tiff, valueOffset + 2, 8);
        WriteTiffShort(tiff, valueOffset + 4, 8);

        return tiff;
    }

    private static void WriteTiffShort(Byte[] buffer, Int32 offset, Int16 value)
    {
        buffer[offset] = (Byte)(value & 0xFF);
        buffer[offset + 1] = (Byte)((value >> 8) & 0xFF);
    }

    private static void WriteTiffLong(Byte[] buffer, Int32 offset, Int32 value)
    {
        buffer[offset] = (Byte)(value & 0xFF);
        buffer[offset + 1] = (Byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (Byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (Byte)((value >> 24) & 0xFF);
    }

    private static void WriteTiffTag(Byte[] buffer, Int32 offset, Int16 tag, Int16 type, Int32 count, Int32 value)
    {
        WriteTiffShort(buffer, offset, tag);
        WriteTiffShort(buffer, offset + 2, type);
        WriteTiffLong(buffer, offset + 4, count);
        WriteTiffLong(buffer, offset + 8, value);
    }

    private static void WriteTiffTag(Byte[] buffer, ref Int32 offset, Int16 tag, Int16 type, Int32 count, Int32 value)
    {
        WriteTiffTag(buffer, offset, tag, type, count, value);
        offset += 12;
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
            ".tif" or ".tiff" => SnapshotFormat.Tiff,
            ".gif" => SnapshotFormat.Gif,
            ".avif" => SnapshotFormat.Avif,
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

    /// <summary>TIFF 格式（FFmpeg 管道编码）</summary>
    Tiff,

    /// <summary>GIF 格式（FFmpeg 管道编码，单帧）</summary>
    Gif,

    /// <summary>AVIF 格式（FFmpeg 管道编码，需 libaom-av1）</summary>
    Avif,
}
