using NewLife.Video.Converters;

namespace NewLife.Video.Processing;

/// <summary>帧缩放处理器。支持最近邻和双线性插值</summary>
/// <remarks>
/// 支持 RGB24、BGRA32、YUV420P 格式。
/// 缩放后在目标格式中保持颜色空间一致。
/// </remarks>
public class FrameResizer : IVideoProcessor
{
    private readonly Int32 _targetWidth;
    private readonly Int32 _targetHeight;

    /// <summary>支持的输入像素格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32, VideoTypes.YUV420P];

    /// <summary>输出格式与输入格式相同</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24; // 默认 RGB24

    /// <summary>创建帧缩放处理器</summary>
    /// <param name="targetWidth">目标宽度</param>
    /// <param name="targetHeight">目标高度</param>
    public FrameResizer(Int32 targetWidth, Int32 targetHeight)
    {
        if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
        if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));

        _targetWidth = targetWidth;
        _targetHeight = targetHeight;
    }

    /// <summary>缩放帧（最近邻插值）</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>缩放后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        // 确保输入为 RGB24
        Byte[] rgbData;
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            rgbData = frame.Data;
        }
        else if (frame.PixelFormat == VideoTypes.YUV420P)
        {
            rgbData = ColorSpaceConverter.YUV420PToRGB24(
                frame.GetYPlane(), frame.GetUPlane(), frame.GetVPlane(),
                frame.Width, frame.Height, frame.YStride, frame.UVStride);
        }
        else if (frame.PixelFormat == VideoTypes.BGRA32)
        {
            rgbData = ColorSpaceConverter.BGRA32ToRGB24(frame.Data, frame.Width, frame.Height);
        }
        else
        {
            throw new NotSupportedException(L.T("Unsupported pixel format: {0}.", frame.PixelFormat));
        }

        // 最近邻缩放
        var result = VideoFrame.Create(_targetWidth, _targetHeight, VideoTypes.RGB24);
        NearestNeighborScale(rgbData, frame.Width, frame.Height, result.Data, _targetWidth, _targetHeight);

        result.Timestamp = frame.Timestamp;
        result.IsKeyFrame = frame.IsKeyFrame;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }

    /// <summary>最近邻插值缩放</summary>
    private static void NearestNeighborScale(Byte[] src, Int32 srcW, Int32 srcH, Byte[] dst, Int32 dstW, Int32 dstH)
    {
        var scaleX = (Double)srcW / dstW;
        var scaleY = (Double)srcH / dstH;

        for (var y = 0; y < dstH; y++)
        {
            var srcY = (Int32)(y * scaleY);
            if (srcY >= srcH) srcY = srcH - 1;

            var srcRow = srcY * srcW * 3;
            var dstRow = y * dstW * 3;

            for (var x = 0; x < dstW; x++)
            {
                var srcX = (Int32)(x * scaleX);
                if (srcX >= srcW) srcX = srcW - 1;

                var srcIdx = srcRow + srcX * 3;
                var dstIdx = dstRow + x * 3;

                dst[dstIdx] = src[srcIdx];       // B
                dst[dstIdx + 1] = src[srcIdx + 1]; // G
                dst[dstIdx + 2] = src[srcIdx + 2]; // R
            }
        }
    }
}
