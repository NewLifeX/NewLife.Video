using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg H.265/HEVC 编码器。通过管道调用 ffmpeg 将 YUV420P 原始帧编码为 H.265 裸流</summary>
public class FFmpegH265Encoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private readonly Int32 _width;
    private readonly Int32 _height;
    private readonly Int32 _bitrate;
    private readonly Int32 _frameRate;

    public String Name => "FFmpeg H.265 Encoder";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.H265];
    public Boolean IsStateful => true;

    public FFmpegH265Encoder(Int32 width, Int32 height, Int32 bitrate = 2000, Int32 frameRate = 25, String ffmpegPath = null)
    {
        _width = width;
        _height = height;
        _bitrate = bitrate;
        _frameRate = frameRate;
        _invoker = new FFmpegInvoker(ffmpegPath);
    }

    public VideoFrame[] Decode(VPacket packet) => throw new NotSupportedException("H.265 编码器不支持解码");

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

        var args = $"-f rawvideo -pix_fmt yuv420p -s {_width}x{_height} -r {_frameRate} -i pipe:0 " +
                   $"-c:v libx265 -preset ultrafast -b:v {_bitrate}k -f hevc pipe:1";
        var encoded = _invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult();

        return new VPacket
        {
            Data = new Packet(encoded ?? []),
            CodecType = VideoTypes.H265,
            PTS = frames[0].Timestamp,
            IsKeyFrame = true,
            FrameIndex = frames[0].FrameIndex,
        };
    }

    public void Dispose() { }
}
