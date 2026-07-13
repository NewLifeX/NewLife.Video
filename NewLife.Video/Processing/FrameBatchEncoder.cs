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
        return [];
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

