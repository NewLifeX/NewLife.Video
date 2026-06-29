using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg VP9 解码器</summary>
public class FFmpegVP9Decoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;

    public String Name => "FFmpeg VP9 Decoder";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.VP9];
    public Boolean IsStateful => true;

    public FFmpegVP9Decoder(Int32 width = 0, Int32 height = 0) { _invoker = new FFmpegInvoker(); _width = width; _height = height; }

    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];
        var data = packet.Data.ReadBytes();
        if (data.Length == 0) return [];

        var args = _width > 0
            ? $"-f ivf -i pipe:0 -f rawvideo -pix_fmt yuv420p -s {_width}x{_height} pipe:1"
            : "-f ivf -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";

        var raw = _invoker.ExecutePipeAsync(args, data).GetAwaiter().GetResult();
        if (raw == null || raw.Length == 0) return [];
        if (_width == 0) (_width, _height) = (640, 480);

        var frameSize = _width * _height * 3 / 2;
        var count = raw.Length / frameSize;
        var frames = new VideoFrame[count];
        for (var i = 0; i < count; i++)
        {
            var f = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
            Array.Copy(raw, i * frameSize, f.Data, 0, Math.Min(frameSize, raw.Length - i * frameSize));
            f.Timestamp = packet.PTS + i * 40; f.FrameIndex = packet.FrameIndex + i;
            frames[i] = f;
        }
        return frames;
    }

    public VPacket Encode(VideoFrame[] frames) => throw new NotSupportedException();
    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;
}
