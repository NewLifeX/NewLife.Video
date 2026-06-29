using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MP4 (ISO BMFF) 容器读取器。纯托管解析 MP4 盒结构</summary>
/// <remarks>
/// MP4 文件结构：ftyp → moov (含 trak → mdia → minf → stbl) → mdat
/// 关键盒：ftyp(文件类型), moov(元数据), mdat(媒体数据), stsd(样本描述), stco/co64(块偏移), stsz(样本大小), stts(时间戳)
/// 当前为骨架实现：解析 ftyp/moov 头、提取视频编码信息和关键参数。
/// 完整 stbl 表解析在后续版本中完善。
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
        _timeScale = (data[12] << 24) | (data[13] << 16) | (data[14] << 8) | data[15];
        var duration64 = (Int64)(data[16] << 24) | (Int64)(data[17] << 16) | (Int64)(data[18] << 8) | data[19];

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
        var entrySize = (Int32)((entryHeader[0] << 24) | (entryHeader[1] << 16) | (entryHeader[2] << 8) | entryHeader[3]);
        var entryType = System.Text.Encoding.ASCII.GetString(entryHeader, 4, 4);

        var entryData = new Byte[entrySize - 8];
        _stream.Read(entryData, 0, entryData.Length);

        switch (entryType)
        {
            case "avc1" or "avc3":
                _codecType = VideoTypes.H264;
                if (entryData.Length >= 24)
                {
                    _videoWidth = (entryData[24] << 8) | entryData[25];
                    _videoHeight = (entryData[26] << 8) | entryData[27];
                }
                break;
            case "hvc1" or "hev1":
                _codecType = VideoTypes.H265;
                if (entryData.Length >= 24)
                {
                    _videoWidth = (entryData[24] << 8) | entryData[25];
                    _videoHeight = (entryData[26] << 8) | entryData[27];
                }
                break;
            case "mp4v":
                _codecType = VideoTypes.MJPEG; // MPEG-4 Visual 近似
                if (entryData.Length >= 24)
                {
                    _videoWidth = (entryData[24] << 8) | entryData[25];
                    _videoHeight = (entryData[26] << 8) | entryData[27];
                }
                break;
        }
    }

    private void ParseStts(Int64 endOffset)
    {
        var data = new Byte[4];
        _stream.Read(data, 0, 4); // version + flags
        _stream.Read(data, 0, 4); // entry count
        var entryCount = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];

        var totalFrames = 0L;
        var totalDuration = 0L;

        for (var i = 0; i < entryCount && i < 1000; i++)
        {
            var entry = new Byte[8];
            _stream.Read(entry, 0, 8);
            var sampleCount = (entry[0] << 24) | (entry[1] << 16) | (entry[2] << 8) | entry[3];
            var sampleDelta = (entry[4] << 24) | (entry[5] << 16) | (entry[6] << 8) | entry[7];

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
        var entryCount = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];

        _chunkOffsets.Clear();
        for (var i = 0; i < entryCount && i < 10000; i++)
        {
            if (is64)
            {
                var offset = new Byte[8];
                _stream.Read(offset, 0, 8);
                _chunkOffsets.Add((Int64)(offset[0] << 56) | (Int64)(offset[1] << 48) | (Int64)(offset[2] << 40) | (Int64)(offset[3] << 32)
                                  | (Int64)(offset[4] << 24) | (Int64)(offset[5] << 16) | (Int64)(offset[6] << 8) | offset[7]);
            }
            else
            {
                var offset = new Byte[4];
                _stream.Read(offset, 0, 4);
                _chunkOffsets.Add((Int64)(offset[0] << 24) | (Int64)(offset[1] << 16) | (Int64)(offset[2] << 8) | offset[3]);
            }
        }
    }

    private void ParseStsz(Int64 endOffset)
    {
        var data = new Byte[8];
        _stream.Read(data, 0, 8); // version + flags + default size
        _defaultSampleSize = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
        _stream.Read(data, 0, 4); // entry count
        var entryCount = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];

        _sampleSizes.Clear();
        if (_defaultSampleSize == 0)
        {
            for (var i = 0; i < entryCount && i < 10000; i++)
            {
                var size = new Byte[4];
                _stream.Read(size, 0, 4);
                _sampleSizes.Add((size[0] << 24) | (size[1] << 16) | (size[2] << 8) | size[3]);
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

            // 读取 NAL 长度前缀 (4字节)
            var lenBytes = new Byte[4];
            if (_stream.Read(lenBytes, 0, 4) < 4) return null;
            var nalLength = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];

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
