namespace NewLife.Video.Processing;

/// <summary>帧率转换处理器。支持抽帧（降帧率）和帧复制（升帧率）</summary>
/// <remarks>
/// 降帧率：按比例跳过帧（N选1）。
/// 升帧率：复制帧填充。
/// 不是 IVideoProcessor（它处理的是帧序列而非单帧）。
/// </remarks>
public class FrameRateConverter
{
    private readonly Double _sourceFps;
    private readonly Double _targetFps;
    private Double _accumulator;
    private VideoFrame _lastFrame;

    /// <summary>源帧率</summary>
    public Double SourceFps => _sourceFps;

    /// <summary>目标帧率</summary>
    public Double TargetFps => _targetFps;

    /// <summary>创建帧率转换器</summary>
    /// <param name="sourceFps">源帧率</param>
    /// <param name="targetFps">目标帧率</param>
    public FrameRateConverter(Double sourceFps, Double targetFps)
    {
        if (sourceFps <= 0) throw new ArgumentOutOfRangeException(nameof(sourceFps));
        if (targetFps <= 0) throw new ArgumentOutOfRangeException(nameof(targetFps));

        _sourceFps = sourceFps;
        _targetFps = targetFps;
    }

    /// <summary>处理下一帧。返回应输出的帧（可能为 null 表示跳过，可能返回同一帧多次）</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>输出帧枚举（可能 0 个、1 个或多个）</returns>
    public IEnumerable<VideoFrame> ProcessFrame(VideoFrame frame)
    {
        if (frame == null) yield break;

        _accumulator += _targetFps / _sourceFps;

        while (_accumulator >= 1.0)
        {
            _accumulator -= 1.0;
            yield return CloneFrame(_lastFrame ?? frame);
        }

        _lastFrame = CloneFrame(frame);
    }

    /// <summary>重置状态</summary>
    public void Reset()
    {
        _accumulator = 0;
        _lastFrame = null;
    }

    private static VideoFrame CloneFrame(VideoFrame source)
    {
        var clone = new VideoFrame
        {
            Width = source.Width,
            Height = source.Height,
            PixelFormat = source.PixelFormat,
            Data = source.Data != null ? (Byte[])source.Data.Clone() : [],
            Timestamp = source.Timestamp,
            IsKeyFrame = source.IsKeyFrame,
            FrameIndex = source.FrameIndex,
            YOffset = source.YOffset,
            UOffset = source.UOffset,
            VOffset = source.VOffset,
            YStride = source.YStride,
            UVStride = source.UVStride,
        };
        return clone;
    }
}
