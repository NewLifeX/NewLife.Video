using NewLife.Data;
using NewLife.Video.FFmpeg;
using NewLife;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg H.265/HEVC 编码器。通过管道调用 ffmpeg 将 YUV420P 原始帧编码为 H.265 裸流</summary>
/// <remarks>
/// 支持通过 <paramref name="encoderName"/> 指定硬件编码器（如 hevc_nvenc/hevc_qsv/hevc_amf），
/// 通过 <paramref name="extraArgs"/> 透传额外 FFmpeg 编码参数。
/// </remarks>
public class FFmpegH265Encoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private readonly Int32 _width;
    private readonly Int32 _height;
    private readonly Int32 _bitrate;
    private readonly Int32 _frameRate;
    private readonly String _encoderName;
    private readonly String _extraArgs;

    public String Name => "FFmpeg H.265 Encoder";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.H265];
    public Boolean IsStateful => true;

    public FFmpegH265Encoder(Int32 width, Int32 height, Int32 bitrate = 2000, Int32 frameRate = 25, String ffmpegPath = null, String encoderName = null, String extraArgs = null)
    {
        _width = width;
        _height = height;
        _bitrate = bitrate;
        _frameRate = frameRate;
        _encoderName = encoderName ?? "libx265";
        _extraArgs = extraArgs ?? "-preset ultrafast";
        _invoker = new FFmpegInvoker(ffmpegPath);
    }

    public VideoFrame[] Decode(VPacket packet) => throw new NotSupportedException(L.T("The H.265 encoder does not support decoding."));

    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0) throw new ArgumentNullException(nameof(frames));

        var frameSize = _width * _height * 3 / 2;
        var rawData = new Byte[frameSize * frames.Length];
        for (var i = 0; i < frames.Length; i++)
        {
            if (frames[i].PixelFormat != VideoTypes.YUV420P)
                throw new NotSupportedException($"H.265 编码器仅支持 YUV420P，当前: {frames[i].PixelFormat}");
            Array.Copy(frames[i].Data, 0, rawData, i * frameSize, Math.Min(frames[i].Data.Length, frameSize));
        }

        // 构建 ffmpeg 参数（支持软编码 libx265 或硬件编码 hevc_nvenc/hevc_qsv/hevc_amf 等）
        var args = $"-f rawvideo -pix_fmt yuv420p -s {_width}x{_height} -r {_frameRate} -i pipe:0 " +
                   $"-c:v {_encoderName} {_extraArgs} -b:v {_bitrate}k -f hevc pipe:1";
        var encoded = _invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult();

        return new VPacket
        {
            Data = new ArrayPacket(encoded ?? []),
            CodecType = VideoTypes.H265,
            PTS = frames[0].Timestamp,
            IsKeyFrame = true,
            FrameIndex = frames[0].FrameIndex,
        };
    }

    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;
}
