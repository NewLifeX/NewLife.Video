using NewLife.Video.Containers;

namespace NewLife.Video.Processing;

/// <summary>缩略图生成器。从视频中提取关键帧并拼接为预览图</summary>
public class ThumbnailGenerator
{
    /// <summary>从视频文件生成缩略图网格</summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="cols">列数</param>
    /// <param name="rows">行数</param>
    /// <param name="thumbWidth">单个缩略图宽度</param>
    /// <param name="thumbHeight">单个缩略图高度</param>
    /// <returns>RGB24 格式的缩略图网格帧</returns>
    public static VideoFrame GenerateGrid(String filePath, Int32 cols = 4, Int32 rows = 3,
        Int32 thumbWidth = 160, Int32 thumbHeight = 90)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(L.T("File not found."), filePath);

        // 提取关键帧
        var keyFrames = FrameExtractor.ExtractKeyFrames(filePath, cols * rows);

        // 创建网格画布
        var canvasW = cols * thumbWidth;
        var canvasH = rows * thumbHeight;
        var canvas = VideoFrame.Create(canvasW, canvasH, VideoTypes.RGB24);

        // 填充黑色背景
        for (var i = 0; i < canvas.Data.Length; i++)
            canvas.Data[i] = 0;

        // 放置缩略图
        var resizer = new FrameResizer(thumbWidth, thumbHeight);
        for (var i = 0; i < keyFrames.Count && i < cols * rows; i++)
        {
            var row = i / cols;
            var col = i % cols;

            // 缩放关键帧
            VideoFrame thumb;
            if (keyFrames[i].PixelFormat == VideoTypes.YUV420P || keyFrames[i].PixelFormat == VideoTypes.NV12)
            {
                // 先转为 RGB
                var rgbFrame = new VideoFrame
                {
                    Width = keyFrames[i].Width,
                    Height = keyFrames[i].Height,
                    PixelFormat = VideoTypes.RGB24,
                };
                // 无法直接转换，跳过
                continue;
            }
            else
            {
                thumb = resizer.ProcessFrame(keyFrames[i]);
            }

            // 复制到画布
            for (var y = 0; y < thumbHeight; y++)
            {
                var srcRow = y * thumbWidth * 3;
                var dstRow = ((row * thumbHeight + y) * canvasW + col * thumbWidth) * 3;
                var copyLen = thumbWidth * 3;
                if (srcRow + copyLen <= thumb.Data.Length && dstRow + copyLen <= canvas.Data.Length)
                    Array.Copy(thumb.Data, srcRow, canvas.Data, dstRow, copyLen);
            }
        }

        return canvas;
    }

    /// <summary>从视频文件生成单帧缩略图（取中间时间点）</summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="width">缩略图宽度</param>
    /// <param name="height">缩略图高度</param>
    /// <returns>缩略图帧</returns>
    public static VideoFrame GenerateSingle(String filePath, Int32 width = 320, Int32 height = 180)
    {
        using var reader = VideoContainerFactory.CreateReader(filePath);
        var midFrame = reader.TotalFrames > 0 ? reader.TotalFrames / 2 : 0;

        var frame = FrameExtractor.ExtractSnapshotByIndex(filePath, midFrame);
        if (frame == null) return null;

        var resizer = new FrameResizer(width, height);
        return resizer.ProcessFrame(frame);
    }
}
