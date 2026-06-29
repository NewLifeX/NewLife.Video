using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg H.265/HEVC 解码器。通过管道调用 ffmpeg 将 H.265 裸流解码为 YUV420P 原始帧</summary>
public class FFmpegH265Decoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;

    public String Name => "FFmpeg H.265 Decoder";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.H265];
    public Boolean IsStateful => true;
    public Int32 Width => _width;
    public Int32 Height => _height;

    public FFmpegH265Decoder(String ffmpegPath = null, Int32 width = 0, Int32 height = 0)
    {
        _invoker = new FFmpegInvoker(ffmpegPath);
        _width = width;
        _height = height;
    }

    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];
        var data = packet.Data.ReadBytes();
        if (data.Length == 0) return [];

        var args = _width > 0
            ? $"-f hevc -i pipe:0 -f rawvideo -pix_fmt yuv420p -s {_width}x{_height} pipe:1"
            : "-f hevc -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";

        var raw = _invoker.ExecutePipeAsync(args, data).GetAwaiter().GetResult();
        if (raw == null || raw.Length == 0) return [];

        if (_width == 0) (_width, _height) = GuessResolution(raw.Length);

        var frameSize = _width * _height * 3 / 2;
        var frameCount = raw.Length / frameSize;
        var frames = new VideoFrame[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frame = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
            Array.Copy(raw, i * frameSize, frame.Data, 0, Math.Min(frameSize, raw.Length - i * frameSize));
            frame.Timestamp = packet.PTS + i * 40;
            frame.IsKeyFrame = packet.IsKeyFrame && i == 0;
            frame.FrameIndex = packet.FrameIndex + i;
            frames[i] = frame;
        }
        return frames;
    }

    public VPacket Encode(VideoFrame[] frames) => throw new NotSupportedException("H.265 解码器不支持编码");

    public void Dispose() { }

    private static (Int32, Int32) GuessResolution(Int32 size)
    {
        foreach (var (w, h) in new[] { (3840, 2160), (1920, 1080), (1280, 720), (640, 480), (352, 288) })
            if (size % (w * h * 3 / 2) == 0) return (w, h);
        return (640, 480);
    }
}
