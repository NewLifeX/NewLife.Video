namespace NewLife.Video.Codecs;

/// <summary>编解码器注册表。管理所有编解码器实例，按 VideoTypes 路由到正确的编解码器</summary>
/// <remarks>
/// 支持运行时动态注册编解码器。无状态编解码器（MJPEG）注册为单例，
/// 有状态编解码器（H.264/H.265）通过工厂委托为每个流创建独立实例。
/// </remarks>
public class CodecRegistry
{
    private readonly Dictionary<VideoTypes, IVideoCodec> _codecs = new();
    private readonly Dictionary<VideoTypes, Func<IVideoCodec>> _factories = new();

    /// <summary>注册一个无状态编解码器实例</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <param name="codec">编解码器实例</param>
    public void Register(VideoTypes videoType, IVideoCodec codec)
    {
        if (codec == null) throw new ArgumentNullException(nameof(codec));
        _codecs[videoType] = codec;
    }

    /// <summary>注册一个有状态编解码器工厂（每次调用创建新实例）</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <param name="factory">编解码器工厂委托</param>
    public void RegisterFactory(VideoTypes videoType, Func<IVideoCodec> factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[videoType] = factory;
    }

    /// <summary>获取或创建指定编码类型的编解码器</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <returns>编解码器实例，未注册则抛异常</returns>
    public IVideoCodec GetCodec(VideoTypes videoType)
    {
        if (_codecs.TryGetValue(videoType, out var codec)) return codec;
        if (_factories.TryGetValue(videoType, out var factory)) return factory();

        throw new NotSupportedException(L.T("No codec is registered for [{0}].", videoType));
    }

    /// <summary>检查是否支持指定编码类型</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <returns></returns>
    public Boolean IsSupported(VideoTypes videoType) => _codecs.ContainsKey(videoType) || _factories.ContainsKey(videoType);

    /// <summary>获取所有已注册的编码类型</summary>
    public IReadOnlyCollection<VideoTypes> RegisteredTypes => [.. _codecs.Keys, .. _factories.Keys];
}
