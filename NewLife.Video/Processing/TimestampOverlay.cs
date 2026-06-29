namespace NewLife.Video.Processing;

/// <summary>时间戳叠加处理器。在视频帧上叠加 OSD 时间戳</summary>
/// <remarks>
/// 内部使用 WatermarkOverlay 实现文本渲染。
/// 支持自定义格式（默认 "yyyy-MM-dd HH:mm:ss"）。
/// </remarks>
public class TimestampOverlay : IVideoProcessor
{
    private readonly String _format;
    private readonly Int32 _x;
    private readonly Int32 _y;
    private readonly Byte _r;
    private readonly Byte _g;
    private readonly Byte _b;
    private DateTime _baseTime;
    private Boolean _useBaseTime;

    /// <summary>支持的输入格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32];

    /// <summary>输出格式</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24;

    /// <summary>创建时间戳叠加处理器</summary>
    /// <param name="format">日期格式</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    public TimestampOverlay(String format = "yyyy-MM-dd HH:mm:ss", Int32 x = 10, Int32 y = 10,
        Byte r = 255, Byte g = 255, Byte b = 255)
    {
        _format = format;
        _x = x;
        _y = y;
        _r = r;
        _g = g;
        _b = b;
    }

    /// <summary>设置基准时间（通常为录像开始时间）</summary>
    /// <param name="baseTime">基准时间</param>
    public void SetBaseTime(DateTime baseTime)
    {
        _baseTime = baseTime;
        _useBaseTime = true;
    }

    /// <summary>清除基准时间，改用帧时间戳</summary>
    public void ClearBaseTime()
    {
        _useBaseTime = false;
    }

    /// <summary>在帧上叠加时间戳</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>叠加时间戳后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        // 计算时间文本
        var timeText = FormatTimestamp(frame.Timestamp);

        // 委托给 WatermarkOverlay
        var overlay = new WatermarkOverlay(timeText, _x, _y, _r, _g, _b);
        var result = overlay.ProcessFrame(frame);

        return result;
    }

    /// <summary>格式化时间戳为字符串</summary>
    private String FormatTimestamp(Int64 timestampMs)
    {
        DateTime time;
        if (_useBaseTime)
        {
            time = _baseTime.AddMilliseconds(timestampMs);
        }
        else
        {
            // 使用当前时间 + 时间戳偏移
            time = DateTime.Now.AddMilliseconds(timestampMs - timestampMs);
            // 如果 timestampMs 有意义（非 0），当作距 Unix epoch 的毫秒数
            if (timestampMs > 0 && timestampMs < 1_000_000_000_000)
            {
                time = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
            }
        }

        return time.ToString(_format);
    }
}
