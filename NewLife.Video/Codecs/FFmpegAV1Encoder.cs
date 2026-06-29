using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg AV1 编码器。将 YUV420P 帧通过 FFmpeg 管道编码为 AV1 (IVF) 流</summary>
/// <remarks>
/// 使用 libaom-av1 或 libsvtav1 软件编码器。
/// libsvtav1 编码速度更快，libaom-av1 压缩率更优。
/// 若 FFmpeg 版本不支持指定编码器，自动回退到默认 AV1 编码器。
/// </remarks>
public class FFmpegAV1Encoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;
    private Double _frameRate;

    /// <summary>编解码器名称</summary>
    public String Name => "FFmpeg AV1 Encoder";

    /// <summary>版本</summary>
    public String Version => "1.0.0";

    /// <summary>支持的编码类型</summary>
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.AV1];

    /// <summary>有状态编解码器</summary>
    public Boolean IsStateful => true;

    /// <summary>视频宽度</summary>
    public Int32 Width => _width;

    /// <summary>视频高度</summary>
    public Int32 Height => _height;

    /// <summary>创建 AV1 编码器</summary>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="frameRate">帧率</param>
    /// <param name="ffmpegPath">FFmpeg 路径</param>
    /// <param name="encoder">编码器名称（libsvtav1 或 libaom-av1，默认 libsvtav1）</param>
    public FFmpegAV1Encoder(Int32 width = 1920, Int32 height = 1080, Double frameRate = 30, String ffmpegPath = null, String encoder = null)
    {
        _invoker = new FFmpegInvoker(ffmpegPath);
        _width = width;
        _height = height;
        _frameRate = frameRate > 0 ? frameRate : 30;
        _encoderName = encoder ?? "libsvtav1";
    }

    private readonly String _encoderName;

    /// <summary>解码（未实现）</summary>
    public VideoFrame[] Decode(VPacket packet)
    {
        throw new NotSupportedException("AV1 编码器不支持解码，请使用 FFmpegAV1Decoder");
    }

    /// <summary>编码 YUV420P 帧为 AV1 流</summary>
    /// <param name="frames">YUV420P 原始帧数组</param>
    /// <returns>AV1 IVF 编码数据包</returns>
    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0)
            return new VPacket { Data = new ArrayPacket([]), CodecType = VideoTypes.AV1 };

        // 合并所有帧为连续 YUV420P 数据
        var frameSize = _width * _height * 3 / 2;
        var rawData = new Byte[frames.Length * frameSize];
        var offset = 0;
        foreach (var f in frames)
        {
            if (f.Data == null) continue;
            var len = Math.Min(f.Data.Length, frameSize);
            Array.Copy(f.Data, 0, rawData, offset, len);
            offset += len;
        }

        // 使用指定编码器，若失败回退到默认 AV1 编码器
        var encoded = TryEncode(rawData, _encoderName);
        if (encoded == null && _encoderName != "libaom-av1")
            encoded = TryEncode(rawData, "libaom-av1");

        return new VPacket
        {
            Data = encoded != null ? new ArrayPacket(encoded) : new ArrayPacket([]),
            CodecType = VideoTypes.AV1,
            IsKeyFrame = true,
            FrameIndex = frames.Length > 0 ? frames[0].FrameIndex : 0,
        };
    }

    private Byte[] TryEncode(Byte[] rawData, String encoder)
    {
        try
        {
            var args = $"-f rawvideo -pix_fmt yuv420p -s {_width}x{_height} -r {_frameRate} -i pipe:0 -c:v {encoder} -f ivf pipe:1";
            return _invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>释放资源</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;
}
