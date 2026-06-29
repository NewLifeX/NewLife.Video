using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MKV/WebM (Matroska) 容器读取器骨架。基于 EBML 格式解析</summary>
/// <remarks>
/// MKV 使用 EBML (Extensible Binary Meta Language) 编码。
/// 关键元素：Segment → Tracks → TrackEntry → Cluster → BlockGroup → Block
/// 当前为骨架实现，提供 EBML 基本解析能力。
/// </remarks>
public class MkvReader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private Int64 _frameIndex;
    private VideoTypes _codecType;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private Double _frameRate;
    private Int64 _totalFrames;
    private Int64 _duration;
    private Int64 _segmentDataStart;

    public VideoTypes VideoCodecType => _codecType;
    public Int32 VideoWidth => _videoWidth;
    public Int32 VideoHeight => _videoHeight;
    public Double FrameRate => _frameRate;
    public Int64 TotalFrames => _totalFrames;
    public Int64 Duration => _duration;

    public MkvReader(String filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _totalFrames = -1;
        _duration = -1;
        _frameRate = 25.0;

        try { ParseEbml(); }
        catch { _stream.Dispose(); throw; }
    }

    private void ParseEbml()
    {
        // 验证 EBML 头
        var header = new Byte[4];
        if (_stream.Read(header, 0, 4) < 4) throw new InvalidOperationException("无效的 MKV 文件");

        if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)
            throw new InvalidOperationException("无效的 EBML 头");

        // 跳过 EBML 头，查找 Segment
        _stream.Seek(0, SeekOrigin.Begin);
        var buffer = new Byte[4096];
        _stream.Read(buffer, 0, buffer.Length);

        // 查找 Segment 元素 (0x18 0x53 0x80 0x67)
        for (var i = 0; i < buffer.Length - 8; i++)
        {
            if (buffer[i] == 0x18 && buffer[i + 1] == 0x53 && buffer[i + 2] == 0x80 && buffer[i + 3] == 0x67)
            {
                // 读取 Segment 大小
                var sizeLen = buffer[i + 4];
                Int64 size = 0;
                for (var j = 0; j < sizeLen; j++)
                    size = (size << 8) | buffer[i + 5 + j];

                _segmentDataStart = i + 4 + sizeLen;
                _totalFrames = size / 5000;
                break;
            }
        }

        _stream.Seek(0, SeekOrigin.Begin);
    }

    public VPacket ReadFrame()
    {
        if (_stream.Position >= _stream.Length) return null;

        // 骨架: 简单读取固定大小数据块
        var data = new Byte[4096];
        var read = _stream.Read(data, 0, data.Length);
        if (read <= 0) return null;

        return new VPacket
        {
            Data = new Packet(data, 0, read),
            CodecType = _codecType,
            PTS = _frameIndex * 40,
            FrameIndex = _frameIndex++,
        };
    }

    public Boolean SeekFrame(Int64 frameIndex) { _frameIndex = frameIndex; return true; }
    public void Dispose() => _stream?.Dispose();
}
