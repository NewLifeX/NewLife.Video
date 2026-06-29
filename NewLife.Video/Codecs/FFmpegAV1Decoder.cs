using NewLife.Data;
using NewLife.Video.FFmpeg;

namespace NewLife.Video.Codecs;

/// <summary>FFmpeg AV1 解码器。通过管道调用 ffmpeg 将 AV1 裸流解码为 YUV420P 原始帧</summary>
/// <remarks>
/// 有状态编解码器（IsStateful=true），每个实例维护独立的 ffmpeg 进程。
/// AV1 使用 IVF 容器封装 OBU 数据送入 ffmpeg 解码。
/// 依赖 FFmpeg 的 libdav1d 或 libaom-av1 解码器。
/// </remarks>
public class FFmpegAV1Decoder : IVideoCodec, ICodecInfo, IDisposable
{
    private readonly FFmpegInvoker _invoker;
    private Int32 _width;
    private Int32 _height;

    /// <summary>编解码器名称</summary>
    public String Name => "FFmpeg AV1 Decoder";

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

    /// <summary>创建 AV1 解码器</summary>
    /// <param name="ffmpegPath">FFmpeg 路径</param>
    /// <param name="width">视频宽度（已知时传入）</param>
    /// <param name="height">视频高度（已知时传入）</param>
    public FFmpegAV1Decoder(String ffmpegPath = null, Int32 width = 0, Int32 height = 0)
    {
        _invoker = new FFmpegInvoker(ffmpegPath);
        _width = width;
        _height = height;
    }

    /// <summary>解码 AV1 数据为 YUV420P 帧</summary>
    /// <param name="packet">AV1 编码数据包（IVF 封装或裸 OBU 流）</param>
    /// <returns>解码后的 YUV420P 帧数组</returns>
    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];

        var data = packet.Data.ToArray();
        if (data.Length == 0) return [];

        // 如果数据不是 IVF 容器格式，尝试自动检测并包装
        var inputData = IsIVFFormat(data) ? data : WrapAsIVF(data);

        // 使用 IVF demuxer + AV1 解码
        var args = _width > 0 && _height > 0
            ? $"-f ivf -i pipe:0 -f rawvideo -pix_fmt yuv420p -s {_width}x{_height} pipe:1"
            : "-f ivf -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";

        var raw = _invoker.ExecutePipeAsync(args, inputData).GetAwaiter().GetResult();

        if (raw == null || raw.Length == 0) return [];

        // 未知分辨率时尝试推定
        if (_width == 0 || _height == 0)
            (_width, _height) = GuessResolution(raw.Length);

        if (_width <= 0 || _height <= 0) return [];

        var frameSize = _width * _height * 3 / 2;
        var frameCount = raw.Length / frameSize;
        var frames = new VideoFrame[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frame = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
            var copyLen = Math.Min(frameSize, raw.Length - i * frameSize);
            Array.Copy(raw, i * frameSize, frame.Data, 0, copyLen);

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
        throw new NotSupportedException("AV1 解码器不支持编码，请使用 FFmpegAV1Encoder");
    }

    /// <summary>释放资源</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; }
    private Boolean _disposed;

    #region 辅助

    /// <summary>检测数据是否为 IVF 容器格式（前4字节应为 DKIF）</summary>
    private static Boolean IsIVFFormat(Byte[] data)
    {
        return data.Length >= 4 && data[0] == 0x44 && data[1] == 0x4B && data[2] == 0x49 && data[3] == 0x46; // "DKIF"
    }

    /// <summary>将裸 AV1 OBU 数据包装为 IVF 容器</summary>
    /// <remarks>生成最小 IVF 头：DKIF 签名 + 版本 + 头长度 + FourCC(AV01) + 宽高 + 帧率 + 帧数 + 数据</remarks>
    private Byte[] WrapAsIVF(Byte[] data)
    {
        using var ms = new System.IO.MemoryStream(data.Length + 32);

        // IVF 文件头 (32 字节)
        ms.Write(System.Text.Encoding.ASCII.GetBytes("DKIF"), 0, 4); // signature
        ms.WriteByte(0); ms.WriteByte(0);                             // version = 0
        ms.WriteByte(32); ms.WriteByte(0);                            // header length = 32
        ms.Write(System.Text.Encoding.ASCII.GetBytes("AV01"), 0, 4);  // FourCC
        WriteUInt16LE(ms, (UInt16)(_width > 0 ? _width : 1920));    // width
        WriteUInt16LE(ms, (UInt16)(_height > 0 ? _height : 1080));  // height
        WriteUInt32LE(ms, 30);                                       // framerate numerator
        WriteUInt32LE(ms, 1);                                        // framerate denominator
        WriteUInt32LE(ms, 1);                                        // frame count
        WriteUInt32LE(ms, 0);                                        // unused

        // 帧数据（1 帧）
        WriteUInt32LE(ms, (UInt32)data.Length); // frame size
        WriteUInt64LE(ms, 0);                    // timestamp
        ms.Write(data, 0, data.Length);

        return ms.ToArray();
    }

    private static void WriteUInt16LE(System.IO.Stream s, UInt16 v)
    {
        s.WriteByte((Byte)(v & 0xFF));
        s.WriteByte((Byte)(v >> 8));
    }

    private static void WriteUInt32LE(System.IO.Stream s, UInt32 v)
    {
        s.WriteByte((Byte)(v & 0xFF));
        s.WriteByte((Byte)((v >> 8) & 0xFF));
        s.WriteByte((Byte)((v >> 16) & 0xFF));
        s.WriteByte((Byte)(v >> 24));
    }

    private static void WriteUInt64LE(System.IO.Stream s, UInt64 v)
    {
        for (var i = 0; i < 8; i++)
            s.WriteByte((Byte)((v >> (i * 8)) & 0xFF));
    }

    private static (Int32, Int32) GuessResolution(Int32 rawSize)
    {
        foreach (var (w, h) in new[] { (3840, 2160), (2560, 1440), (1920, 1080), (1280, 720), (960, 540), (640, 480), (352, 288) })
            if (rawSize % (w * h * 3 / 2) == 0) return (w, h);
        return (1920, 1080);
    }

    #endregion
}
