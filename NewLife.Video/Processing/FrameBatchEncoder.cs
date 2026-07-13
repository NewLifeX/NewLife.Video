using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NewLife.Video.Processing;

/// <summary>批量帧图像编码管线。将帧序列批量编码为多格式图像序列或动画 GIF/WebP/TIFF</summary>
/// <remarks>
/// 适用于缩略图墙、视频预览图生成等场景。
/// 支持通过 ImageSharp 和 SkiaSharp 进行图像编码，需确保对应 NuGet 包已安装。
/// </remarks>
public static class FrameBatchEncoder
{
    /// <summary>批量编码帧为指定格式的字节数组序列</summary>
    /// <param name="frames">视频帧序列</param>
    /// <param name="format">目标图像格式</param>
    /// <param name="quality">质量参数（JPEG/WebP/AVIF 等使用）</param>
    /// <returns>图像字节数组序列</returns>
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
                case BatchFormat.Tiff:
                    data = SnapshotEncoder.EncodeToTiff(frame);
                    break;
                case BatchFormat.Avif:
                    data = SkiaSharpExtensions.EncodeToAvif(frame, quality);
                    break;
                case BatchFormat.Heif:
                    data = SkiaSharpExtensions.EncodeToHeif(frame, quality);
                    break;
                default:
                    data = SnapshotEncoder.EncodeToJpeg(frame, quality);
                    break;
            }
            yield return data;
        }
    }

    /// <summary>批量编码帧为图像文件</summary>
    /// <param name="frames">视频帧序列</param>
    /// <param name="outputDir">输出目录</param>
    /// <param name="format">目标格式</param>
    /// <param name="baseName">文件名前缀</param>
    /// <param name="quality">质量参数</param>
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

    /// <summary>生成网格缩略图（将多帧拼接为一个大图）</summary>
    /// <param name="frames">视频帧序列</param>
    /// <param name="columns">列数</param>
    /// <param name="thumbnailWidth">每帧缩略图宽度</param>
    /// <param name="thumbnailHeight">每帧缩略图高度</param>
    /// <returns>网格缩略图字节数组（JPEG）</returns>
    public static Byte[] CreateGridThumbnail(IEnumerable<VideoFrame> frames, Int32 columns = 4, Int32 thumbnailWidth = 160, Int32 thumbnailHeight = 120)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));

        var frameList = frames.Where(f => f?.Data != null).ToList();
        if (frameList.Count == 0) return [];

        var rows = (Int32)Math.Ceiling((Double)frameList.Count / columns);
        var gridWidth = columns * thumbnailWidth;
        var gridHeight = rows * thumbnailHeight;

        using var gridImage = new Image<Rgba32>(gridWidth, gridHeight);

        for (var i = 0; i < frameList.Count; i++)
        {
            using var thumb = frameList[i].ToImageSharpImage();
            thumb.Mutate(x => x.Resize(thumbnailWidth, thumbnailHeight));

            var col = i % columns;
            var row = i / columns;
            gridImage.Mutate(x => x.DrawImage(thumb, new Point(col * thumbnailWidth, row * thumbnailHeight), 1f));
        }

        using var ms = new MemoryStream();
        gridImage.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static String GetExtension(BatchFormat format) => format switch
    {
        BatchFormat.Jpeg => "jpg",
        BatchFormat.Png => "png",
        BatchFormat.WebP => "webp",
        BatchFormat.Tiff => "tiff",
        BatchFormat.Avif => "avif",
        BatchFormat.Heif => "heif",
        _ => "jpg",
    };
}

/// <summary>批量编码图像格式</summary>
public enum BatchFormat
{
    /// <summary>JPEG</summary>
    Jpeg,

    /// <summary>PNG</summary>
    Png,

    /// <summary>WebP</summary>
    WebP,

    /// <summary>TIFF</summary>
    Tiff,

    /// <summary>AVIF（需 SkiaSharp）</summary>
    Avif,

    /// <summary>HEIF（需 SkiaSharp）</summary>
    Heif,
}
