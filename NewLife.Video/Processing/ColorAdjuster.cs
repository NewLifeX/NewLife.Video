namespace NewLife.Video.Processing;

/// <summary>色彩调整处理器。调整亮度、对比度、饱和度</summary>
/// <remarks>支持 RGB24 和 BGRA32 格式输入</remarks>
public class ColorAdjuster : IVideoProcessor
{
    /// <summary>亮度调整（0.0=不变，正数变亮，负数变暗）</summary>
    public Double Brightness { get; set; }

    /// <summary>对比度调整（1.0=不变，&gt;1增强，&lt;1减弱）</summary>
    public Double Contrast { get; set; }

    /// <summary>饱和度调整（1.0=不变，&gt;1增强，&lt;1减弱）</summary>
    public Double Saturation { get; set; }

    /// <summary>支持的输入格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32];

    /// <summary>输出格式</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24;

    /// <summary>创建色彩调整处理器</summary>
    /// <param name="brightness">亮度调整值</param>
    /// <param name="contrast">对比度系数</param>
    /// <param name="saturation">饱和度系数</param>
    public ColorAdjuster(Double brightness = 0, Double contrast = 1.0, Double saturation = 1.0)
    {
        Brightness = brightness;
        Contrast = contrast;
        Saturation = saturation;
    }

    /// <summary>处理帧</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>调整后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        var bytesPerPixel = frame.PixelFormat == VideoTypes.BGRA32 ? 4 : 3;
        var pixelCount = frame.Width * frame.Height;
        var result = VideoFrame.Create(frame.Width, frame.Height, VideoTypes.RGB24);

        for (var i = 0; i < pixelCount; i++)
        {
            var srcIdx = i * bytesPerPixel;
            var r = (Double)frame.Data[srcIdx + 2]; // R
            var g = (Double)frame.Data[srcIdx + 1]; // G
            var b = (Double)frame.Data[srcIdx];     // B

            // 亮度调整
            r += Brightness * 255;
            g += Brightness * 255;
            b += Brightness * 255;

            // 对比度调整
            r = (r - 128) * Contrast + 128;
            g = (g - 128) * Contrast + 128;
            b = (b - 128) * Contrast + 128;

            // 饱和度调整
            if (Math.Abs(Saturation - 1.0) > 0.001)
            {
                var gray = r * 0.299 + g * 0.587 + b * 0.114;
                r = gray + (r - gray) * Saturation;
                g = gray + (g - gray) * Saturation;
                b = gray + (b - gray) * Saturation;
            }

            // 钳位
            var dstIdx = i * 3;
            result.Data[dstIdx] = Clamp(b);
            result.Data[dstIdx + 1] = Clamp(g);
            result.Data[dstIdx + 2] = Clamp(r);
        }

        result.Timestamp = frame.Timestamp;
        result.IsKeyFrame = frame.IsKeyFrame;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }

    private static Byte Clamp(Double value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return (Byte)value;
    }
}
