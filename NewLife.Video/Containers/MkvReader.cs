using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MKV/WebM (Matroska) 容器读取器。基于 EBML 格式解析</summary>
/// <remarks>
/// 解析 EBML → Segment → Tracks/TrackEntry → Cluster → SimpleBlock/BlockGroup。
/// 支持 VP8/VP9/H.264/H.265 编码和 Matroska/WebM 文件。
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
    private Int64 _segmentEnd;
    private Int64 _clusterPos;
    private readonly List<BlockEntry> _blocks = [];
    private Byte[] _trackCodecPrivate;
    private String _trackCodecId;

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
        // 跳过 EBML 头，查找 Segment
        _stream.Seek(0, SeekOrigin.Begin);

        while (_stream.Position + 4 <= _stream.Length)
        {
            var (id, size) = ReadEbmlElement();
            if (id == 0) break;

            if (id == 0x1A45DFA3) // EBML header
            {
                _stream.Seek(size, SeekOrigin.Current);
                continue;
            }
            if (id == 0x18538067) // Segment
            {
                _segmentEnd = size == Int64.MaxValue ? _stream.Length : _stream.Position + size;
                ParseSegment();
                break;
            }
            // 跳过未知元素
            if (size <= 0 || _stream.Position + size > _stream.Length) break;
            _stream.Seek(size, SeekOrigin.Current);
        }
    }

    private void ParseSegment()
    {
        while (_stream.Position < _segmentEnd)
        {
            var pos = _stream.Position;
            var (id, size) = ReadEbmlElement();
            if (id == 0) break;
            var endPos = _stream.Position + size;

            switch (id)
            {
                case 0x1549A966: // Info
                    ParseInfo(size);
                    break;
                case 0x1654AE6B: // Tracks
                    ParseTracks(size);
                    break;
                case 0x1F43B675: // Cluster
                    ParseCluster(size);
                    break;
                default:
                    _stream.Seek(size, SeekOrigin.Current);
                    break;
            }

            if (endPos > _segmentEnd) break;
        }

        _totalFrames = _blocks.Count;
    }

    private void ParseInfo(Int64 size)
    {
        var end = _stream.Position + size;
        while (_stream.Position < end)
        {
            var (id, sz) = ReadEbmlElement();
            if (id == 0) break;

            if (id == 0x2AD7B1) // TimecodeScale
            {
                _frameRate = 1_000_000_000.0 / ReadEbmlUint(sz);
            }
            else if (id == 0x4489) // Duration (float)
            {
                _duration = (Int64)(ReadEbmlFloat(sz) * 1000);
            }
            else _stream.Seek(sz, SeekOrigin.Current);

            if (_stream.Position >= end) break;
        }
        _stream.Seek(end, SeekOrigin.Begin);
    }

    private void ParseTracks(Int64 size)
    {
        var end = _stream.Position + size;
        while (_stream.Position < end)
        {
            var (id, sz) = ReadEbmlElement();
            if (id == 0xAE) // TrackEntry
            {
                ParseTrackEntry(sz);
            }
            else _stream.Seek(sz, SeekOrigin.Current);

            if (_stream.Position >= end) break;
        }
        _stream.Seek(end, SeekOrigin.Begin);
    }

    private void ParseTrackEntry(Int64 size)
    {
        var end = _stream.Position + size;
        Int64 trackType = 0;

        while (_stream.Position < end)
        {
            var (id, sz) = ReadEbmlElement();
            if (id == 0) break;

            switch (id)
            {
                case 0x83: trackType = (Int64)ReadEbmlUint(sz); break; // TrackType
                case 0x86: _trackCodecId = ReadEbmlString(sz); break; // CodecID
                case 0xE0: _videoWidth = (Int32)ReadEbmlUint(sz); break; // PixelWidth
                case 0xBA: _videoHeight = (Int32)ReadEbmlUint(sz); break; // PixelHeight
                case 0x63A2: _trackCodecPrivate = ReadEbmlBinary(sz); break; // CodecPrivate
                default: _stream.Seek(sz, SeekOrigin.Current); break;
            }
            if (_stream.Position >= end) break;
        }

        if (trackType == 1) // video
        {
            _codecType = _trackCodecId switch
            {
                "V_VP8" => VideoTypes.VP8,
                "V_VP9" => VideoTypes.VP9,
                "V_MPEG4/ISO/AVC" => VideoTypes.H264,
                "V_MPEGH/ISO/HEVC" => VideoTypes.H265,
                _ => VideoTypes.Unknown,
            };
        }

        _stream.Seek(end, SeekOrigin.Begin);
    }

    private void ParseCluster(Int64 size)
    {
        var end = _stream.Position + size;
        Int64 clusterTimecode = 0;

        while (_stream.Position < end)
        {
            var (id, sz) = ReadEbmlElement();
            if (id == 0) break;

            switch (id)
            {
                case 0xE7: clusterTimecode = (Int64)ReadEbmlUint(sz); break; // Timecode
                case 0xA0: // BlockGroup
                    ParseBlockGroup(sz, clusterTimecode);
                    break;
                case 0xA3: // SimpleBlock
                    ParseSimpleBlock(sz, clusterTimecode);
                    break;
                default:
                    _stream.Seek(sz, SeekOrigin.Current);
                    break;
            }
            if (_stream.Position >= end) break;
        }

        _stream.Seek(end, SeekOrigin.Begin);
    }

    private void ParseSimpleBlock(Int64 size, Int64 clusterTimecode)
    {
        var start = _stream.Position;
        var trackNumber = ReadEbmlVint();       // Track Number
        var timecode = ReadInt16BE();            // Timecode (relative to cluster)
        var flags = _stream.ReadByte();          // flags
        var dataLen = (Int32)(size - (_stream.Position - start));

        if (dataLen > 0 && dataLen < 100 * 1024 * 1024)
        {
            var data = new Byte[dataLen];
            _stream.Read(data, 0, dataLen);
            _blocks.Add(new BlockEntry
            {
                Data = data,
                PTS = clusterTimecode + timecode,
                IsKeyFrame = (flags & 0x80) != 0,
            });
        }

        _stream.Seek(start + size, SeekOrigin.Begin);
    }

    private void ParseBlockGroup(Int64 size, Int64 clusterTimecode)
    {
        var end = _stream.Position + size;
        Byte[] blockData = null;
        Int64 blockPts = clusterTimecode;
        Boolean isKeyFrame = true;

        while (_stream.Position < end)
        {
            var (id, sz) = ReadEbmlElement();
            if (id == 0) break;

            if (id == 0xA1) // Block
            {
                var blockStart = _stream.Position;
                ReadEbmlVint(); // track number
                var timecode = ReadInt16BE();
                var flags = _stream.ReadByte();
                var dataLen = (Int32)(sz - (_stream.Position - blockStart));
                if (dataLen > 0 && dataLen < 100 * 1024 * 1024)
                {
                    blockData = new Byte[dataLen];
                    _stream.Read(blockData, 0, dataLen);
                    blockPts = clusterTimecode + timecode;
                    isKeyFrame = (flags & 0x80) == 0;
                }
            }
            else _stream.Seek(sz, SeekOrigin.Current);

            if (_stream.Position >= end) break;
        }

        if (blockData != null)
        {
            _blocks.Add(new BlockEntry
            {
                Data = blockData,
                PTS = blockPts,
                IsKeyFrame = isKeyFrame,
            });
        }

        _stream.Seek(end, SeekOrigin.Begin);
    }

    public VPacket ReadFrame()
    {
        if (_frameIndex >= _blocks.Count) return null;
        var block = _blocks[(Int32)_frameIndex];

        return new VPacket
        {
            Data = new Packet(block.Data),
            CodecType = _codecType,
            PTS = block.PTS,
            FrameIndex = _frameIndex++,
            IsKeyFrame = block.IsKeyFrame,
        };
    }

    public Boolean SeekFrame(Int64 frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _blocks.Count) return false;
        _frameIndex = frameIndex;
        return true;
    }

    public void Dispose() => _stream?.Dispose();

    #region EBML 工具

    private (UInt32 Id, Int64 Size) ReadEbmlElement()
    {
        var first = _stream.ReadByte();
        if (first < 0) return (0, 0);

        // EBML VINT 编码：检查首字节是否有前导 1（否则为原始 4 字节 ID）
        UInt32 id;
        if ((first & 0x80) != 0)
        {
            var idLen = 1;
            var mask = (Byte)first;
            while ((mask & 0x40) != 0) { mask <<= 1; idLen++; }

            id = (UInt32)(first & (0x7F >> (idLen - 1)));
            for (var i = 1; i < idLen; i++)
            {
                var b = _stream.ReadByte();
                if (b < 0) return (0, 0);
                id = (id << 8) | (UInt32)b;
            }
        }
        else
        {
            // 兼容原始 4 字节 ID（MkvWriter 产生的格式）
            id = (UInt32)first;
            for (var i = 0; i < 3; i++)
            {
                var b = _stream.ReadByte();
                if (b < 0) return (0, 0);
                id = (id << 8) | (UInt32)b;
            }
        }

        // 读取 Element Size (EBML VINT)
        first = _stream.ReadByte();
        if (first < 0) return (0, 0);

        var sizeLen = 1;
        var mask2 = (Byte)first;
        while ((mask2 & 0x80) == 0) { mask2 <<= 1; sizeLen++; }

        Int64 size = (first & (0xFF >> sizeLen));
        for (var i = 1; i < sizeLen; i++)
        {
            var b = _stream.ReadByte();
            if (b < 0) return (0, 0);
            size = (size << 8) | b;
        }

        // Unknown size: all 1s
        if ((first & (0xFF >> (sizeLen - 1))) == (0xFF >> (sizeLen - 1)))
        {
            var allOnes = true;
            for (var i = 0; i < sizeLen - 1; i++)
                if (((size >> (i * 8)) & 0xFF) != 0xFF) allOnes = false;
            if (allOnes && sizeLen > 1) size = Int64.MaxValue;
        }

        return (id, size);
    }

    private Int64 ReadEbmlVint()
    {
        var first = _stream.ReadByte();
        if (first < 0) return 0;

        var len = 1;
        var mask = (Byte)first;
        while ((mask & 0x80) == 0) { mask <<= 1; len++; }

        Int64 value = (first & (0xFF >> len));
        for (var i = 1; i < len; i++)
        {
            var b = _stream.ReadByte();
            if (b < 0) break;
            value = (value << 8) | b;
        }
        return value;
    }

    private UInt64 ReadEbmlUint(Int64 size)
    {
        if (size <= 0 || size > 8) return 0;
        UInt64 value = 0;
        for (var i = 0; i < size; i++)
        {
            var b = _stream.ReadByte();
            if (b < 0) break;
            value = (value << 8) | (UInt64)b;
        }
        return value;
    }

    private Double ReadEbmlFloat(Int64 size)
    {
        if (size == 4)
        {
            var bytes = new Byte[4];
            _stream.Read(bytes, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
        if (size == 8)
        {
            var bytes = new Byte[8];
            _stream.Read(bytes, 0, 8);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }
        return 0;
    }

    private String ReadEbmlString(Int64 size)
    {
        if (size <= 0 || size > 4096) return "";
        var bytes = new Byte[size];
        _stream.Read(bytes, 0, (Int32)size);
        return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    private Byte[] ReadEbmlBinary(Int64 size)
    {
        if (size <= 0 || size > 1024 * 1024) return [];
        var bytes = new Byte[size];
        _stream.Read(bytes, 0, (Int32)size);
        return bytes;
    }

    private Int16 ReadInt16BE()
    {
        var hi = _stream.ReadByte();
        var lo = _stream.ReadByte();
        return (Int16)((hi << 8) | lo);
    }

    private sealed class BlockEntry
    {
        public Byte[] Data;
        public Int64 PTS;
        public Boolean IsKeyFrame;
    }

    #endregion
}

