using NewLife.Data;
using NewLife.Video.ChipHeaders;

namespace NewLife.Video.Codecs;

/// <summary>视频编解码工厂。管理编解码器注册表、芯片头处理链，提供统一的编解码入口</summary>
/// <remarks>
/// 自动注册内置编解码器。支持运行时通过 <see cref="Register"/> 方法注册自定义编解码器。
/// 芯片头处理链：自动尝试去除已知芯片头（海思/大华/宇视），编码时按需还原。
/// </remarks>
public class VideoCodecFactory
{
    private readonly CodecRegistry _registry = new();
    private readonly List<IVideoChipHeader> _chipHeaders = [];
    private IVideoChipHeader _lastChipHeader;

    /// <summary>编解码器注册表</summary>
    public CodecRegistry Registry => _registry;

    /// <summary>芯片头处理器列表</summary>
    public IReadOnlyList<IVideoChipHeader> ChipHeaders => _chipHeaders;

    /// <summary>初始化编解码工厂，自动注册所有内置解码器</summary>
    /// <remarks>编码器需通过 <see cref="Register"/> 或 <see cref="RegisterFactory"/> 手动注册</remarks>
    public VideoCodecFactory()
    {
        // 纯托管编解码器（无外部依赖，同时支持编解码）
        Register(VideoTypes.MJPEG, new Codecs.MjpegCodec());

        // FFmpeg 解码器（需要 ffmpeg 在 PATH 中；编码器需手动注册）
        RegisterFactory(VideoTypes.H264, () => new Codecs.FFmpegH264Decoder());
        RegisterFactory(VideoTypes.H265, () => new Codecs.FFmpegH265Decoder());
        RegisterFactory(VideoTypes.VP8, () => new Codecs.FFmpegVP8Decoder());
        RegisterFactory(VideoTypes.VP9, () => new Codecs.FFmpegVP9Decoder());
        RegisterFactory(VideoTypes.AV1, () => new Codecs.FFmpegAV1Decoder());
    }

    /// <summary>注册自定义编解码器</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <param name="codec">编解码器实例</param>
    public void Register(VideoTypes videoType, IVideoCodec codec) => _registry.Register(videoType, codec);

    /// <summary>注册有状态编解码器工厂</summary>
    /// <param name="videoType">视频编码类型</param>
    /// <param name="factory">工厂委托</param>
    public void RegisterFactory(VideoTypes videoType, Func<IVideoCodec> factory) => _registry.RegisterFactory(videoType, factory);

    /// <summary>注册芯片头处理器</summary>
    /// <param name="header">芯片头处理器</param>
    public void RegisterChipHeader(IVideoChipHeader header)
    {
        if (header == null) throw new ArgumentNullException(nameof(header));
        _chipHeaders.Add(header);
        _chipHeaders.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>去除芯片头。按优先级顺序尝试所有芯片头处理器</summary>
    /// <param name="data">设备数据</param>
    /// <returns>去除芯片头后的数据</returns>
    public IPacket TrimChipHeader(IPacket data)
    {
        foreach (var header in _chipHeaders)
        {
            if (header.TryTrim(data, out var result))
            {
                _lastChipHeader = header;
                return result;
            }
        }

        _lastChipHeader = null;
        return data;
    }

    /// <summary>添加芯片头（还原最近一次去除的芯片头）</summary>
    /// <param name="data">原始编码数据</param>
    /// <returns>添加芯片头后的数据</returns>
    public IPacket AddChipHeader(IPacket data)
    {
        if (_lastChipHeader != null && _lastChipHeader.TryAdd(data, out var result))
            return result;

        return data;
    }

    /// <summary>解码视频数据包为原始帧</summary>
    /// <param name="packet">编码数据包</param>
    /// <returns>解码后的视频帧数组</returns>
    /// <exception cref="NotSupportedException">未注册对应编码类型时抛出</exception>
    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));
        if (packet.Data == null) return [];

        // 去除芯片头
        var data = TrimChipHeader(packet.Data);
        var pkt = new VPacket(data, packet.CodecType)
        {
            PTS = packet.PTS,
            DTS = packet.DTS,
            IsKeyFrame = packet.IsKeyFrame,
            FrameIndex = packet.FrameIndex,
        };

        var codec = _registry.GetCodec(packet.CodecType);
        return codec.Decode(pkt);
    }

    /// <summary>编码原始帧为视频数据包</summary>
    /// <param name="frames">原始视频帧数组</param>
    /// <param name="videoType">目标编码类型</param>
    /// <returns>编码后的数据包</returns>
    /// <exception cref="NotSupportedException">未注册对应编码类型时抛出</exception>
    public VPacket Encode(VideoFrame[] frames, VideoTypes videoType)
    {
        if (frames == null || frames.Length == 0) throw new ArgumentNullException(nameof(frames));

        var codec = _registry.GetCodec(videoType);
        var packet = codec.Encode(frames);

        // 如果有上次去除的芯片头，还原之
        packet.Data = AddChipHeader(packet.Data);

        return packet;
    }
}
