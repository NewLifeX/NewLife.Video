using NewLife.Data;

namespace NewLife.Video.Processing;

/// <summary>批量帧图像编码管线。将帧序列批量编码为多格式图像文件，纯托管 + FFmpeg 实现</summary>
/// <remarks>
/// 适用于缩略图墙、视频预览图生成等场景。
/// 所有图像编码基于纯托管 MJPEG/像素操作 和 FFmpeg 管道，无外部图像库依赖。
/// </remarks>
public static class FrameBatchEncoder
{
    /// <summary>批量编码帧为指定格式的字节数组序列</summary>
    public static IEnumerable<Byte[]> EncodeBatch(IEnumerable<VideoFrame> frames, BatchFormat format, Int32 quality = 80)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));

        foreach (var frame in frames)
        {
            if (frame?.Data == null) continue;

            Byte[] data;
            switch (format)
            {
                case BatchFormat.Jpeg:
                    data = SnapshotEncoder.EncodeToJpeg(frame, quality);
                    break;
                case BatchFormat.Png:
                    data = SnapshotEncoder.EncodeToPng(frame);
                    break;
                case BatchFormat.WebP:
                    data = SnapshotEncoder.EncodeToWebP(frame, quality);
                    break;
                case BatchFormat.Bmp:
                    data = SnapshotEncoder.EncodeToBmp(frame);
                    break;
                case BatchFormat.Tiff:
                    data = SnapshotEncoder.EncodeToTiff(frame);
                    break;
                case BatchFormat.Gif:
                    data = SnapshotEncoder.EncodeToGif(frame);
                    break;
                case BatchFormat.Avif:
                    data = SnapshotEncoder.EncodeToAvif(frame, quality);
                    break;
                default:
                    data = SnapshotEncoder.EncodeToJpeg(frame, quality);
                    break;
            }
            yield return data;
        }
    }

    /// <summary>批量编码帧为图像文件</summary>
    public static void EncodeBatchToFiles(IEnumerable<VideoFrame> frames, String outputDir, BatchFormat format, String baseName = "frame", Int32 quality = 80)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));
        if (String.IsNullOrEmpty(outputDir)) throw new ArgumentNullException(nameof(outputDir));

        Directory.CreateDirectory(outputDir);
        var ext = GetExtension(format);
        var index = 0;

        foreach (var data in EncodeBatch(frames, format, quality))
        {
            var path = Path.Combine(outputDir, $"{baseName}_{index:D4}.{ext}");
            File.WriteAllBytes(path, data);
            index++;
        }
    }

    /// <summary>生成网格缩略图（将多帧拼为一张大图 JPEG）</summary>
    public static Byte[] CreateGridThumbnail(IEnumerable<VideoFrame> frames, Int32 columns = 4, Int32 thumbnailWidth = 160, Int32 thumbnailHeight = 120)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));

        var frameList = frames.Where(f => f?.Data != null).ToList();
        if (frameList.Count == 0) return [];

        var rows = (Int32)Math.Ceiling((Double)frameList.Count / columns);
        var gridWidth = columns * thumbnailWidth;
        var gridHeight = rows * thumbnailHeight;

        // 通过 FFmpeg 拼接缩略图
        return BuildGridViaFFmpeg(frameList, columns, rows, gridWidth, gridHeight, thumbnailWidth, thumbnailHeight);
    }

    private static Byte[] BuildGridViaFFmpeg(List<VideoFrame> frames, Int32 columns, Int32 rows, Int32 gridW, Int32 gridH, Int32 thumbW, Int32 thumbH)
    {
        var invoker = new FFmpeg.FFmpegInvoker();
        if (!invoker.IsAvailable()) return [];

        try
        {
            // 将每帧写入临时文件（或通过管道），使用 FFmpeg tile 滤镜合成网格
            // 参考 FFmpeg 文档: filtergraph = [0:v]scale=160:120[s0];[1:v]scale=160:120[s1];[s0][s1]xstack=grid=2x1
            var tempDir = Path.Combine(Path.GetTempPath(), "ff_grid_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var inputPaths = "";
                for (var i = 0; i < frames.Count; i++)
                {
                    // 将每帧存为临时 JPEG（使用纯托管编码器）
                    var jpeg = SnapshotEncoder.EncodeToJpeg(frames[i], 85);
                    var tmpFile = Path.Combine(tempDir, $"f{i}.jpg");
                    File.WriteAllBytes(tmpFile, jpeg);
                    inputPaths += $"-i \"{tmpFile}\" ";
                }

                // 构建 tile 滤镜
                var scaleFilters = String.Join(";\n",
                    frames.Select((_, i) => $"[{i}:v]scale={thumbW}:{thumbH}[s{i}]"));
                var inputs = String.Join("",
                    frames.Select((_, i) => $"[s{i}]"));
                var filter = $"\"{scaleFilters};\n{inputs}xstack=grid={columns}x{rows}:fill=black[v]\"";

                var args = $"{inputPaths}-filter_complex {filter} -map \"[v]\" -frames:v 1 -f image2 pipe:1";
                var result = invoker.ExecutePipeAsync(args, null).GetAwaiter().GetResult();
                return result ?? [];
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        catch
        {
            return [];
        }
    }

    private static String GetExtension(BatchFormat format) => format switch
    {
        BatchFormat.Jpeg => "jpg",
        BatchFormat.Png => "png",
        BatchFormat.WebP => "webp",
        BatchFormat.Bmp => "bmp",
        BatchFormat.Tiff => "tiff",
        BatchFormat.Gif => "gif",
        BatchFormat.Avif => "avif",
        _ => "jpg",
    };
}

/// <summary>批量编码图像格式</summary>
public enum BatchFormat
{
    /// <summary>JPEG（纯托管）</summary>
    Jpeg,
    /// <summary>PNG（FFmpeg）</summary>
    Png,
    /// <summary>WebP（FFmpeg）</summary>
    WebP,
    /// <summary>BMP（纯托管）</summary>
    Bmp,
    /// <summary>TIFF（FFmpeg）</summary>
    Tiff,
    /// <summary>GIF（FFmpeg）</summary>
    Gif,
    /// <summary>AVIF（FFmpeg）</summary>
    Avif,
}

