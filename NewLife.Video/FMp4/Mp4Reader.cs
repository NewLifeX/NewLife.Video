using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.FMp4;

/// <summary>MP4 (ISO BMFF) 容器读取器。纯托管解析 MP4 盒结构</summary>
/// <remarks>
/// MP4 文件结构：ftyp → moov (含 trak → mdia → minf → stbl) → mdat
/// 关键盒：ftyp(文件类型), moov(元数据), mdat(媒体数据), stsd(样本描述), stco/co64(块偏移), stsz(样本大小), stts(时间戳)
/// 已解析 stbl 样本表（stsd/stts/stco/co64/stsz），提取编码类型、宽高、帧率与块偏移；
/// 读取时按块偏移逐块读取 NAL 长度前缀样本，未使用 stsc 样本-块映射，适用于单样本单块的常规 MP4。
/// </remarks>
public class Mp4Reader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private Int64 _frameIndex;
    private VideoTypes _codecType;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private Double _frameRate;
    private Int64 _totalFrames;
    private Int64 _duration;

    // stbl 表数据
    private readonly List<Int64> _chunkOffsets = [];
    private readonly List<Int32> _sampleSizes = [];
    private readonly List<Int64> _sampleTimestamps = [];
    private Int32 _timeScale;
    private Int32 _defaultSampleSize;
    private Int32 _samplesPerChunk;
    private Boolean _tablesParsed;

    /// <summary>视频编码类型</summary>
    public VideoTypes VideoCodecType => _codecType;

    /// <summary>视频宽度</summary>
    public Int32 VideoWidth => _videoWidth;

    /// <summary>视频高度</summary>
    public Int32 VideoHeight => _videoHeight;

    /// <summary>帧率</summary>
    public Double FrameRate => _frameRate;

    /// <summary>总帧数</summary>
    public Int64 TotalFrames => _totalFrames;

    /// <summary>时长（毫秒）</summary>
    public Int64 Duration => _duration;

    /// <summary>创建 MP4 读取器</summary>
    /// <param name="filePath">MP4 文件路径</param>
    public Mp4Reader(String filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);

        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _totalFrames = -1;
        _duration = -1;
        _frameRate = 25.0;

        try
        {
            ParseBoxes();
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    private void ParseBoxes()
    {
        while (_stream.Position < _stream.Length)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            switch (box.Type)
            {
                case "ftyp":
                    ParseFtyp();
                    break;
                case "moov":
                    ParseMoov(boxEnd);
                    break;
                case "mdat":
                    _tablesParsed = true;
                    return; // 找到 mdat，停止解析
            }

            // 跳到盒末尾
            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private (Int64 Size, String Type) ReadBoxHeader()
    {
        var header = new Byte[8];
        if (_stream.Read(header, 0, 8) < 8)
            return (0, null);

        var reader = new SpanReader(header) { IsLittleEndian = false };
        var size = (Int64)reader.ReadUInt32();
        var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);

        // 扩展大小（64位）
        if (size == 1)
        {
            var extSize = new Byte[8];
            _stream.Read(extSize, 0, 8);
            var extReader = new SpanReader(extSize) { IsLittleEndian = false };
            size = extReader.ReadInt64();
        }
        else if (size == 0)
        {
            size = _stream.Length - _stream.Position + 8; // 到文件末尾
        }

        return (size, type);
    }

    private void ParseFtyp()
    {
        var majorBrand = new Byte[4];
        _stream.Read(majorBrand, 0, 4);
        // 忽略 minor version 和 compatible brands
    }

    private void ParseMoov(Int64 endOffset)
    {
        while (_stream.Position < endOffset - 8)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0 || _stream.Position - 8 + box.Size > _stream.Length) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            if (box.Type == "trak")
            {
                ParseTrak(boxEnd);
            }

            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private void ParseTrak(Int64 endOffset)
    {
        while (_stream.Position < endOffset - 8)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            switch (box.Type)
            {
                case "mdia":
                    ParseMdia(boxEnd);
                    break;
            }

            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private void ParseMdia(Int64 endOffset)
    {
        while (_stream.Position < endOffset - 8)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            switch (box.Type)
            {
                case "mdhd":
                    ParseMdhd();
                    break;
                case "minf":
                    ParseMinf(boxEnd);
                    break;
            }

            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private void ParseMinf(Int64 endOffset)
    {
        while (_stream.Position < endOffset - 8)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            switch (box.Type)
            {
                case "stbl":
                    ParseStbl(boxEnd);
                    break;
                case "vmhd":
                case "smhd":
                    break;
            }

            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private void ParseStbl(Int64 endOffset)
    {
        while (_stream.Position < endOffset - 8)
        {
            var box = ReadBoxHeader();
            if (box.Size <= 0) break;

            var boxEnd = _stream.Position - 8 + box.Size;

            switch (box.Type)
            {
                case "stsd":
                    ParseStsd(boxEnd);
                    break;
                case "stts":
                    ParseStts(boxEnd);
                    break;
                case "stco":
                    ParseStco(boxEnd, is64: false);
                    break;
                case "co64":
                    ParseStco(boxEnd, is64: true);
                    break;
                case "stsz":
                    ParseStsz(boxEnd);
                    break;
            }

            if (_stream.Position < boxEnd)
                _stream.Seek(boxEnd, SeekOrigin.Begin);
        }
    }

    private void ParseMdhd()
    {
        var data = new Byte[20];
        _stream.Read(data, 0, data.Length);
        // version(1) + flags(3) + creation(4) + modification(4)
        var reader = new SpanReader(data) { IsLittleEndian = false };
        reader.ReadBytes(12);
        _timeScale = (Int32)reader.ReadUInt32();
        var duration64 = (Int64)reader.ReadUInt32();

        if (_timeScale > 0)
            _duration = duration64 * 1000 / _timeScale;
    }

    private void ParseStsd(Int64 endOffset)
    {
        var data = new Byte[4];
        _stream.Read(data, 0, 4); // version + flags
        _stream.Read(data, 0, 4); // entry count

        // 读取第一个条目
        var entryHeader = new Byte[8];
        _stream.Read(entryHeader, 0, 8);
        var entryReader = new SpanReader(entryHeader) { IsLittleEndian = false };
        var entrySize = (Int32)entryReader.ReadUInt32();
        var entryType = System.Text.Encoding.ASCII.GetString(entryHeader, 4, 4);

        var entryData = new Byte[entrySize - 8];
        _stream.Read(entryData, 0, entryData.Length);

        switch (entryType)
        {
            case "avc1" or "avc3":
                _codecType = VideoTypes.H264;
                if (entryData.Length >= 28)
                    ParseVisualSampleEntry(entryData);
                break;
            case "hvc1" or "hev1":
                _codecType = VideoTypes.H265;
                if (entryData.Length >= 28)
                    ParseVisualSampleEntry(entryData);
                break;
            case "mp4v":
                _codecType = VideoTypes.MJPEG; // MPEG-4 Visual 近似
                if (entryData.Length >= 28)
                    ParseVisualSampleEntry(entryData);
                break;
        }
    }

    private void ParseVisualSampleEntry(Byte[] entryData)
    {
        var reader = new SpanReader(entryData) { IsLittleEndian = false };
        reader.ReadBytes(24); // 跳过到 width/height (VisualSampleEntry 固定偏移 24)
        _videoWidth = reader.ReadUInt16();
        _videoHeight = reader.ReadUInt16();
    }

    private void ParseStts(Int64 endOffset)
    {
        var buf = new Byte[8]; // 循环外分配，复用
        var data = new Byte[4];
        _stream.Read(data, 0, 4); // version + flags
        _stream.Read(data, 0, 4); // entry count
        var countReader = new SpanReader(data) { IsLittleEndian = false };
        var entryCount = (Int32)countReader.ReadUInt32();

        var totalFrames = 0L;
        var totalDuration = 0L;

        for (var i = 0; i < entryCount && i < 1000; i++)
        {
            _stream.Read(buf, 0, 8);
            var reader = new SpanReader(buf) { IsLittleEndian = false };
            var sampleCount = (Int32)reader.ReadUInt32();
            var sampleDelta = (Int32)reader.ReadUInt32();

            totalFrames += sampleCount;
            totalDuration += sampleCount * sampleDelta;
        }

        _totalFrames = totalFrames;
        if (_timeScale > 0 && totalFrames > 0)
        {
            _frameRate = (Double)totalFrames / (totalDuration / (Double)_timeScale);
        }
    }

    private void ParseStco(Int64 endOffset, Boolean is64)
    {
        var data = new Byte[4];
        _stream.Read(data, 0, 4); // version + flags
        _stream.Read(data, 0, 4); // entry count
        var countReader = new SpanReader(data) { IsLittleEndian = false };
        var entryCount = (Int32)countReader.ReadUInt32();

        _chunkOffsets.Clear();
        var buf = new Byte[8]; // 循环外分配，复用（最大 8 字节）
        for (var i = 0; i < entryCount && i < 10000; i++)
        {
            if (is64)
            {
                _stream.Read(buf, 0, 8);
                var reader = new SpanReader(buf) { IsLittleEndian = false };
                _chunkOffsets.Add(reader.ReadInt64());
            }
            else
            {
                _stream.Read(buf, 0, 4);
                var reader = new SpanReader(buf) { IsLittleEndian = false };
                _chunkOffsets.Add(reader.ReadUInt32());
            }
        }
    }

    private void ParseStsz(Int64 endOffset)
    {
        var data = new Byte[8];
        _stream.Read(data, 0, 8); // version + flags + default size
        var hdrReader = new SpanReader(data) { IsLittleEndian = false };
        hdrReader.ReadBytes(4);
        _defaultSampleSize = (Int32)hdrReader.ReadUInt32();

        _stream.Read(data, 0, 4); // entry count
        var countReader = new SpanReader(data) { IsLittleEndian = false };
        var entryCount = (Int32)countReader.ReadUInt32();

        _sampleSizes.Clear();
        if (_defaultSampleSize == 0)
        {
            var sizeBuf = new Byte[4]; // 循环外分配，复用
            for (var i = 0; i < entryCount && i < 10000; i++)
            {
                _stream.Read(sizeBuf, 0, 4);
                var reader = new SpanReader(sizeBuf) { IsLittleEndian = false };
                _sampleSizes.Add((Int32)reader.ReadUInt32());
            }
        }
    }

    /// <summary>读取下一帧</summary>
    public VPacket ReadFrame()
    {
        if (_frameIndex >= _chunkOffsets.Count && _chunkOffsets.Count > 0)
            return null;

        if (_chunkOffsets.Count > 0 && _frameIndex < _chunkOffsets.Count)
        {
            // 跳转到块偏移
            _stream.Seek(_chunkOffsets[(Int32)_frameIndex], SeekOrigin.Begin);

            // 读取 NAL 长度前缀 (4字节，大端)
            var lenBytes = new Byte[4];
            if (_stream.Read(lenBytes, 0, 4) < 4) return null;
            var lenReader = new SpanReader(lenBytes) { IsLittleEndian = false };
            var nalLength = (Int32)lenReader.ReadUInt32();

            if (nalLength <= 0 || nalLength > 10 * 1024 * 1024) return null;

            var nalData = new Byte[nalLength];
            var read = _stream.Read(nalData, 0, nalLength);
            if (read <= 0) return null;

            var packet = new VPacket
            {
                Data = new ArrayPacket(nalData, 0, read),
                CodecType = _codecType,
                PTS = _frameIndex * 40, // 25fps 基准
                FrameIndex = _frameIndex,
                IsKeyFrame = _frameIndex == 0,
            };

            _frameIndex++;
            return packet;
        }

        return null;
    }

    /// <summary>跳转到指定帧</summary>
    public Boolean SeekFrame(Int64 frameIndex)
    {
        if (frameIndex < 0 || _chunkOffsets.Count == 0) return false;
        if (frameIndex >= _chunkOffsets.Count) return false;

        _frameIndex = frameIndex;
        return true;
    }

    /// <summary>释放资源</summary>
    public void Dispose() => _stream?.Dispose();
}
