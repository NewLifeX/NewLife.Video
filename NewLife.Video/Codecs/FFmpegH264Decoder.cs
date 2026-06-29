using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg H.264 解码器。通过管道调用 ffmpeg 将 H.264 裸流解码为 YUV420P 原始帧</summary>
/// <remarks>
/// 有状态编解码器（IsStateful=true），每个实例维护独立的 ffmpeg 进程。
/// 使用管道通信：stdin 输入 H.264 裸流 → stdout 输出 YUV420P 原始帧。
/// </remarks>
public class FFmpegH264Decoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;

    /// <summary>编解码器名称</summary>
    public String Name => "FFmpeg H.264 Decoder";

    /// <summary>版本</summary>
    public String Version => "1.0.0";

    /// <summary>支持的编码类型</summary>
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.H264];

    /// <summary>有状态编解码器</summary>
    public Boolean IsStateful => true;

    /// <summary>视频宽度（解码后可知）</summary>
    public Int32 Width => _width;

    /// <summary>视频高度（解码后可知）</summary>
    public Int32 Height => _height;

    /// <summary>创建 H.264 解码器</summary>
    /// <param name="ffmpegPath">FFmpeg 路径</param>
    /// <param name="width">视频宽度（已知时传入）</param>
    /// <param name="height">视频高度（已知时传入）</param>
    public FFmpegH264Decoder(String ffmpegPath = null, Int32 width = 0, Int32 height = 0)
    {
        _invoker = new FFmpegInvoker(ffmpegPath);
        _width = width;
        _height = height;
    }

    /// <summary>解码 H.264 数据为 YUV420P 帧</summary>
    /// <param name="packet">H.264 编码数据包（裸流，不含容器）</param>
    /// <returns>解码后的 YUV420P 帧数组</returns>
    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];

        var data = packet.Data.ReadBytes();
        if (data.Length == 0) return [];

        // 构建 ffmpeg 参数
        var args = "-f h264 -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";
        if (_width > 0 && _height > 0)
            args = $"-f h264 -i pipe:0 -f rawvideo -pix_fmt yuv420p -s {_width}x{_height} pipe:1";

        var raw = _invoker.ExecutePipeAsync(args, data).GetAwaiter().GetResult();

        if (raw == null || raw.Length == 0) return [];

        // 如果不知道尺寸，从数据量反推（假设 YUV420P）
        if (_width == 0 || _height == 0)
        {
            // 尝试常见分辨率
            foreach (var (w, h) in CommonResolutions)
            {
                var expectedSize = w * h * 3 / 2;
                if (raw.Length % expectedSize == 0)
                {
                    _width = w;
                    _height = h;
                    break;
                }
            }
        }

        if (_width <= 0 || _height <= 0) return [];

        var frameSize = _width * _height * 3 / 2;
        var frameCount = raw.Length / frameSize;
        var frames = new VideoFrame[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frameData = new Byte[frameSize];
            Array.Copy(raw, i * frameSize, frameData, 0, frameSize);

            var frame = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
            Array.Copy(frameData, frame.Data, Math.Min(frameData.Length, frame.Data.Length));

            frame.Timestamp = packet.PTS + i * 40;
            frame.IsKeyFrame = packet.IsKeyFrame && i == 0;
            frame.FrameIndex = packet.FrameIndex + i;

            frames[i] = frame;
        }

        return frames;
    }

    /// <summary>编码（未实现）</summary>
    public VPacket Encode(VideoFrame[] frames)
    {
        throw new NotSupportedException("H.264 解码器不支持编码，请使用 FFmpegH264Encoder");
    }

    /// <summary>释放资源</summary>
    public void Dispose() { }

    private static readonly (Int32 W, Int32 H)[] CommonResolutions =
    [
        (3840, 2160), (2560, 1440), (1920, 1080), (1280, 720),
        (704, 576), (640, 480), (352, 288), (320, 240),
    ];
}
