namespace NewLife.Video.Processing;

/// <summary>帧裁剪处理器</summary>
public class FrameCropper : IVideoProcessor
{
    private readonly Int32 _x;
    private readonly Int32 _y;
    private readonly Int32 _width;
    private readonly Int32 _height;

    /// <summary>支持的输入像素格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32];

    /// <summary>输出格式（与输入相同）</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24;

    /// <summary>创建帧裁剪处理器</summary>
    /// <param name="x">裁剪起始 X</param>
    /// <param name="y">裁剪起始 Y</param>
    /// <param name="width">裁剪宽度</param>
    /// <param name="height">裁剪高度</param>
    public FrameCropper(Int32 x, Int32 y, Int32 width, Int32 height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    /// <summary>裁剪帧</summary>
    /// <param name="frame">输入帧（RGB24 或 BGRA32）</param>
    /// <returns>裁剪后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        var bytesPerPixel = frame.PixelFormat == VideoTypes.BGRA32 ? 4 : 3;
        var result = VideoFrame.Create(_width, _height, VideoTypes.RGB24);

        for (var row = 0; row < _height; row++)
        {
            var srcY = _y + row;
            if (srcY < 0 || srcY >= frame.Height) continue;

            var srcRow = srcY * frame.Width * bytesPerPixel;
            var dstRow = row * _width * 3;

            for (var col = 0; col < _width; col++)
            {
                var srcX = _x + col;
                if (srcX < 0 || srcX >= frame.Width) continue;

                var srcIdx = srcRow + srcX * bytesPerPixel;
                var dstIdx = dstRow + col * 3;

                // 输出 RGB24（BGRA 截断 Alpha）
                result.Data[dstIdx] = frame.Data[srcIdx];       // B
                result.Data[dstIdx + 1] = frame.Data[srcIdx + 1]; // G
                result.Data[dstIdx + 2] = frame.Data[srcIdx + 2]; // R
            }
        }

        result.Timestamp = frame.Timestamp;
        result.IsKeyFrame = frame.IsKeyFrame;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }
}
