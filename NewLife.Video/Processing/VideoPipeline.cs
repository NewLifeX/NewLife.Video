namespace NewLife.Video.Processing;

/// <summary>视频处理管线。链式组合多个 IVideoProcessor，自身也是 IVideoProcessor</summary>
/// <remarks>
/// 使用流畅 API：new VideoPipeline().Append(resizer).Append(adjuster).ProcessFrame(frame)
/// 支持动态添加/移除处理器。
/// </remarks>
public class VideoPipeline : IVideoProcessor
{
    private readonly List<IVideoProcessor> _processors = [];

    /// <summary>支持的输入格式（取第一个处理器的支持格式，若无处理器则支持所有）</summary>
    public VideoTypes[] SupportedInputFormats =>
        _processors.Count > 0 ? _processors[0].SupportedInputFormats : [];

    /// <summary>输出格式（取最后一个处理器的输出格式，若无处理器则原样输出）</summary>
    public VideoTypes OutputFormat =>
        _processors.Count > 0 ? _processors[^1].OutputFormat : VideoTypes.RGB24;

    /// <summary>添加处理器到管线末尾</summary>
    /// <param name="processor">处理器</param>
    /// <returns>当前管线（支持链式调用）</returns>
    public VideoPipeline Append(IVideoProcessor processor)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));
        _processors.Add(processor);
        return this;
    }

    /// <summary>移除处理器</summary>
    /// <param name="processor">要移除的处理器</param>
    /// <returns>当前管线</returns>
    public VideoPipeline Remove(IVideoProcessor processor)
    {
        _processors.Remove(processor);
        return this;
    }

    /// <summary>获取管线的处理器数量</summary>
    public Int32 Count => _processors.Count;

    /// <summary>处理视频帧，依次经过所有处理器</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>处理后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        var current = frame;
        foreach (var processor in _processors)
        {
            current = processor.ProcessFrame(current);
        }
        return current;
    }
}
