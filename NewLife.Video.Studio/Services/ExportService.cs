using NewLife.Video.Processing;

namespace NewLife.Video.Studio.Services;

/// <summary>导出服务实现</summary>
public class ExportService : IExportService
{
    /// <summary>保存快照</summary>
    public Byte[] SaveSnapshot(NewLife.Video.VideoFrame frame, SnapshotFormat format = SnapshotFormat.Jpeg, Int32 quality = 85)
    {
        return format switch
        {
            SnapshotFormat.Png => SnapshotEncoder.EncodeToPng(frame),
            SnapshotFormat.WebP => SnapshotEncoder.EncodeToWebP(frame, quality),
            SnapshotFormat.Gif => SnapshotEncoder.EncodeToGif(frame),
            SnapshotFormat.Bmp => SnapshotEncoder.EncodeToBmp(frame),
            _ => SnapshotEncoder.EncodeToJpeg(frame, quality),
        };
    }

    /// <summary>生成缩略图网格</summary>
    public VideoFrame GenerateThumbnailGrid(String filePath, Int32 cols = 4, Int32 rows = 3, Int32 thumbWidth = 160, Int32 thumbHeight = 90)
        => ThumbnailGenerator.GenerateGrid(filePath, cols, rows, thumbWidth, thumbHeight);

    /// <summary>生成单帧缩略图</summary>
    public VideoFrame GenerateThumbnailSingle(String filePath, Int32 width = 320, Int32 height = 180)
        => ThumbnailGenerator.GenerateSingle(filePath, width, height);

    /// <summary>编码为 PNG</summary>
    public Byte[] EncodePng(NewLife.Video.VideoFrame frame) => PngEncoder.EncodeFrame(frame);

    /// <summary>编码为动画 GIF</summary>
    public Byte[] EncodeAnimatedGif(IReadOnlyList<Byte[]> frames, Int32 width, Int32 height, Int32 delayCentiseconds = 10, Int32 loopCount = 0)
        => GifEncoder.EncodeAnimation(frames, width, height, delayCentiseconds, loopCount);

    /// <summary>编码为 WebP</summary>
    public Byte[] EncodeWebP(NewLife.Video.VideoFrame frame) => WebPEncoder.EncodeFrame(frame);
}
