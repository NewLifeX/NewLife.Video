using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg VP8 编码器。将 YUV420P 帧通过 FFmpeg 管道编码为 IVF/VP8 流</summary>
public class FFmpegVP8Encoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;

    public String Name => "FFmpeg VP8 Encoder";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.VP8];
    public Boolean IsStateful => true;

    public FFmpegVP8Encoder(Int32 width = 640, Int32 height = 480)
    {
        _invoker = new FFmpegInvoker();
        _width = width;
        _height = height;
    }

    public VideoFrame[] Decode(VPacket packet) => throw new NotSupportedException();

    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0) return new VPacket { Data = new ArrayPacket(new Byte[0]), CodecType = VideoTypes.VP8 };

        var rawData = new Byte[frames.Length * _width * _height * 3 / 2];
        var offset = 0;
        foreach (var f in frames)
        {
            if (f.Data == null) continue;
            var len = Math.Min(f.Data.Length, _width * _height * 3 / 2);
            Array.Copy(f.Data, 0, rawData, offset, len);
            offset += len;
        }

        var args = $"-f rawvideo -pix_fmt yuv420p -s {_width}x{_height} -r 25 -i pipe:0 -c:v libvpx -f ivf pipe:1";
        var result = _invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult();

        return new VPacket
        {
            Data = result != null ? new ArrayPacket(result) : new ArrayPacket(new Byte[0]),
            CodecType = VideoTypes.VP8,
            IsKeyFrame = true,
            FrameIndex = frames.Length > 0 ? frames[0].FrameIndex : 0
        };
    }

    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;
}
