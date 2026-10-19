using NewLife.Data;
using NewLife.Video.FFmpeg;
using NewLife;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg H.264 编码器。通过管道调用 ffmpeg 将 YUV420P 原始帧编码为 H.264 裸流</summary>
/// <remarks>
/// 有状态编解码器（IsStateful=true），每个实例维护独立的编码参数。
/// 使用管道通信：stdin 输入 YUV420P 原始帧 → stdout 输出 H.264 裸流。
/// 支持通过 <paramref name="encoderName"/> 指定硬件编码器（如 h264_nvenc/h264_qsv/h264_amf），
/// 通过 <paramref name="extraArgs"/> 透传额外 FFmpeg 编码参数。
/// </remarks>
public class FFmpegH264Encoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private readonly Int32 _width;
    private readonly Int32 _height;
    private readonly Int32 _bitrate;
    private readonly Int32 _frameRate;
    private readonly String _encoderName;
    private readonly String _extraArgs;

    /// <summary>编解码器名称</summary>
    public String Name => "FFmpeg H.264 Encoder";

    /// <summary>版本</summary>
    public String Version => "1.0.0";

    /// <summary>支持的编码类型</summary>
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.H264];

    /// <summary>有状态编解码器</summary>
    public Boolean IsStateful => true;

    /// <summary>创建 H.264 编码器</summary>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="bitrate">码率（kbps，默认 2000）</param>
    /// <param name="frameRate">帧率（默认 25）</param>
    /// <param name="ffmpegPath">FFmpeg 路径（可空，自动查找）</param>
    /// <param name="encoderName">编码器名称（默认 libx264，可指定 h264_nvenc/h264_qsv/h264_amf 等）</param>
    /// <param name="extraArgs">额外 FFmpeg 编码参数，追加到 -c:v 之后，如 "-preset medium -profile:v high -g 30"</param>
    public FFmpegH264Encoder(Int32 width, Int32 height, Int32 bitrate = 2000, Int32 frameRate = 25, String ffmpegPath = null, String encoderName = null, String extraArgs = null)
    {
        _width = width;
        _height = height;
        _bitrate = bitrate;
        _frameRate = frameRate;
        _encoderName = encoderName ?? "libx264";
        _extraArgs = extraArgs ?? "-preset ultrafast";
        _invoker = new FFmpegInvoker(ffmpegPath);
    }

    /// <summary>解码（未实现）</summary>
    public VideoFrame[] Decode(VPacket packet)
    {
        throw new NotSupportedException(L.T("The H.264 encoder does not support decoding; use FFmpegH264Decoder."));
    }

    /// <summary>编码 YUV420P 帧为 H.264 裸流</summary>
    /// <param name="frames">YUV420P 帧数组</param>
    /// <returns>H.264 编码数据包</returns>
    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0)
            throw new ArgumentNullException(nameof(frames));

        // 拼接所有帧为连续 YUV420P 数据
        var frameSize = _width * _height * 3 / 2;
        var rawData = new Byte[frameSize * frames.Length];

        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            Byte[] frameData;

            if (frame.PixelFormat == VideoTypes.YUV420P)
            {
                frameData = frame.Data;
            }
            else
            {
                throw new NotSupportedException(L.T("The H.264 encoder only supports YUV420P input; current format: {0}.", frame.PixelFormat));
            }

            var copyLen = Math.Min(frameData.Length, frameSize);
            Array.Copy(frameData, 0, rawData, i * frameSize, copyLen);
        }

        // 构建 ffmpeg 参数（支持软编码 libx264 或硬件编码 h264_nvenc/h264_qsv/h264_amf 等）
        var args = $"-f rawvideo -pix_fmt yuv420p -s {_width}x{_height} -r {_frameRate} -i pipe:0 " +
                   $"-c:v {_encoderName} {_extraArgs} -b:v {_bitrate}k -f h264 pipe:1";

        var encoded = _invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult();

        return new VPacket
        {
            Data = encoded != null ? new ArrayPacket(encoded) : new ArrayPacket(Array.Empty<Byte>()),
            CodecType = VideoTypes.H264,
            PTS = frames.Length > 0 ? frames[0].Timestamp : 0,
            IsKeyFrame = true,
            FrameIndex = frames.Length > 0 ? frames[0].FrameIndex : 0,
        };
    }

    /// <summary>释放资源</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;
}
