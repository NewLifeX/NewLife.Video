using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.Raw;

/// <summary>原始视频帧读取器。按帧读取无容器的原始像素数据（YUV420P/NV12/RGB24等）</summary>
/// <remarks>
/// IoT 场景中摄像头常直接输出原始 YUV 数据，无任何容器封装。
/// 本读取器按固定帧大小逐帧读取原始像素数据。
/// </remarks>
public class RawVideoReader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private readonly Int32 _frameSize;
    private Int64 _frameIndex;

    /// <summary>视频编码类型（原始数据，非压缩编码）</summary>
    public VideoTypes VideoCodecType => _pixelFormat;

    /// <summary>视频宽度</summary>
    public Int32 VideoWidth { get; }

    /// <summary>视频高度</summary>
    public Int32 VideoHeight { get; }

    /// <summary>帧率（默认 25fps）</summary>
    public Double FrameRate => 25.0;

    /// <summary>总帧数</summary>
    public Int64 TotalFrames { get; }

    /// <summary>时长（毫秒）</summary>
    public Int64 Duration => TotalFrames > 0 ? (Int64)(TotalFrames / FrameRate * 1000) : -1;

    private readonly VideoTypes _pixelFormat;

    /// <summary>创建原始视频帧读取器</summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="pixelFormat">像素格式</param>
    public RawVideoReader(String filePath, Int32 width, Int32 height, VideoTypes pixelFormat)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(L.T("File not found."), filePath);

        VideoWidth = width;
        VideoHeight = height;
        _pixelFormat = pixelFormat;
        _frameSize = VideoFrame.CalculateDataSize(width, height, pixelFormat);
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        TotalFrames = _stream.Length / _frameSize;
    }

    /// <summary>读取下一帧</summary>
    /// <returns>视频数据包，流末尾返回 null</returns>
    public VPacket ReadFrame()
    {
        if (_stream.Position >= _stream.Length) return null;

        var buffer = new Byte[_frameSize];
        var read = _stream.Read(buffer, 0, _frameSize);
        if (read == 0) return null;

        var packet = new VPacket
        {
            Data = new ArrayPacket(buffer, 0, read),
            CodecType = _pixelFormat,
            PTS = _frameIndex * 40, // 25fps → 40ms 每帧
            FrameIndex = _frameIndex,
            IsKeyFrame = true,
        };

        _frameIndex++;
        return packet;
    }

    /// <summary>跳转到指定帧</summary>
    /// <param name="frameIndex">帧索引</param>
    /// <returns>是否成功</returns>
    public Boolean SeekFrame(Int64 frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= TotalFrames) return false;

        _stream.Seek(frameIndex * _frameSize, SeekOrigin.Begin);
        _frameIndex = frameIndex;
        return true;
    }

    /// <summary>释放资源</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; _stream?.Dispose(); }
    private Boolean _disposed;
}
