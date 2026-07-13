using NewLife.Video.Processing;

namespace NewLife.Video.Studio.Services;

/// <summary>导出服务接口（M6-1/2）</summary>
public interface IExportService
{
    /// <summary>保存快照</summary>
    Byte[] SaveSnapshot(NewLife.Video.VideoFrame frame, SnapshotFormat format = SnapshotFormat.Jpeg, Int32 quality = 85);

    /// <summary>生成缩略图网格</summary>
    VideoFrame GenerateThumbnailGrid(String filePath, Int32 cols = 4, Int32 rows = 3, Int32 thumbWidth = 160, Int32 thumbHeight = 90);

    /// <summary>生成单帧缩略图</summary>
    VideoFrame GenerateThumbnailSingle(String filePath, Int32 width = 320, Int32 height = 180);

    /// <summary>编码为 PNG</summary>
    Byte[] EncodePng(NewLife.Video.VideoFrame frame);

    /// <summary>编码为动画 GIF</summary>
    Byte[] EncodeAnimatedGif(IReadOnlyList<Byte[]> frames, Int32 width, Int32 height, Int32 delayCentiseconds = 10, Int32 loopCount = 0);

    /// <summary>编码为 WebP</summary>
    Byte[] EncodeWebP(NewLife.Video.VideoFrame frame);
}
