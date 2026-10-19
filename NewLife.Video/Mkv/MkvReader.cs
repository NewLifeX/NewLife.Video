using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.Mkv;

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
        if (!File.Exists(filePath)) throw new FileNotFoundException(L.T("File not found."), filePath);
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

            // 若 TrackEntry 未直接提供宽高，尝试从 CodecPrivate 解析
            if ((_videoWidth == 0 || _videoHeight == 0) && _trackCodecPrivate != null)
                ParseCodecPrivateForDimensions();
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
                    isKeyFrame = (flags & 0x80) != 0;
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
            Data = new ArrayPacket(block.Data),
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

    #region CodecPrivate 解析

    /// <summary>从 CodecPrivate（AVCC/HVCC）中回退提取宽高</summary>
    private void ParseCodecPrivateForDimensions()
    {
        if (_trackCodecPrivate == null || _trackCodecPrivate.Length < 7) return;
        if (_codecType == VideoTypes.H264)
            ParseAvccForDimensions();
        else if (_codecType == VideoTypes.H265)
            ParseHvccForDimensions();
    }

    private void ParseAvccForDimensions()
    {
        var data = _trackCodecPrivate;
        if (data.Length < 7 || data[0] != 1) return;

        var numSps = data[5] & 0x1F;
        var offset = 6;
        for (var i = 0; i < numSps && offset + 2 <= data.Length; i++)
        {
            var reader = new SpanReader(data, offset) { IsLittleEndian = false };
            var spsLen = reader.ReadUInt16();
            offset += 2;
            if (offset + spsLen <= data.Length && spsLen >= 4)
            {
                ParseSpsForDimensions(data.AsSpan(offset, spsLen), isH264: true);
                if (_videoWidth > 0 && _videoHeight > 0) return;
            }
            offset += spsLen;
        }
    }

    private void ParseHvccForDimensions()
    {
        var data = _trackCodecPrivate;
        if (data.Length < 23) return;

        var offset = 22;
        var numArrays = data[offset++];
        for (var a = 0; a < numArrays && offset + 3 <= data.Length; a++)
        {
            var nalType = data[offset] & 0x3F;
            var numNalus = (Int32)(((UInt32)data[offset + 1] << 8) | data[offset + 2]);
            offset += 3;
            for (var n = 0; n < numNalus && offset + 2 <= data.Length; n++)
            {
                var nalLen = (Int32)(((UInt32)data[offset] << 8) | data[offset + 1]);
                offset += 2;
                if (nalType == 33 && offset + nalLen <= data.Length && nalLen >= 4)
                {
                    ParseSpsForDimensions(data.AsSpan(offset, nalLen), isH264: false);
                    if (_videoWidth > 0 && _videoHeight > 0) return;
                }
                offset += nalLen;
            }
        }
    }

    /// <summary>从 H.264/H.265 SPS 中简化提取宽高</summary>
    private void ParseSpsForDimensions(Span<Byte> sps, Boolean isH264)
    {
        if (sps.Length < 4) return;
        try
        {
            var bitReader = new BitReader(sps, isH264 ? 4 * 8 : 3 * 8);

            if (isH264)
            {
                var profileIdc = sps[1];
                bitReader.ReadUeGolomb(); // seq_parameter_set_id
                if (profileIdc >= 100)
                {
                    var chromaFormatIdc = bitReader.ReadUeGolomb();
                    if (chromaFormatIdc == 3) bitReader.SkipBits(1);
                    bitReader.ReadUeGolomb();
                    bitReader.ReadUeGolomb();
                    bitReader.SkipBits(1);
                    if (bitReader.ReadBit() != 0)
                    {
                        var mc = chromaFormatIdc != 3 ? 8 : 12;
                        for (var m = 0; m < mc; m++)
                            if (bitReader.ReadBit() != 0)
                            {
                                var ssl = m < 6 ? 16 : 64; var ls = 8; var ns = 8;
                                for (var j = 0; j < ssl; j++)
                                { if (ns != 0) { var ds = bitReader.ReadSeGolomb(); ns = (ls + ds + 256) % 256; } ls = ns == 0 ? ls : ns; }
                            }
                    }
                }
                bitReader.ReadUeGolomb();
                var pct = bitReader.ReadUeGolomb();
                if (pct == 0) bitReader.ReadUeGolomb();
                else if (pct == 1) { bitReader.SkipBits(1); bitReader.ReadSeGolomb(); bitReader.ReadSeGolomb(); var nr = bitReader.ReadUeGolomb(); for (var r = 0; r < nr; r++) bitReader.ReadSeGolomb(); }
                bitReader.ReadUeGolomb();
                bitReader.SkipBits(1);
                _videoWidth = (bitReader.ReadUeGolomb() + 1) * 16;
                _videoHeight = (bitReader.ReadUeGolomb() + 1) * 16;
                if (bitReader.ReadBit() == 0) _videoHeight *= 2;
            }
            else // H.265 简化
            {
                bitReader.SkipBits(4);
                var msl = bitReader.ReadBits(3);
                bitReader.SkipBits(1 + 80);
                bitReader.ReadUeGolomb();
                var cfi = bitReader.ReadUeGolomb();
                if (cfi == 3) bitReader.SkipBits(1);
                _videoWidth = bitReader.ReadUeGolomb();
                _videoHeight = bitReader.ReadUeGolomb();
            }
        }
        catch { }
    }

    /// <summary>位读取器（ref struct）</summary>
    private ref struct BitReader
    {
        private readonly ReadOnlySpan<Byte> _data;
        private Int32 _bitPosition;
        public BitReader(ReadOnlySpan<Byte> data, Int32 startBit) { _data = data; _bitPosition = startBit; }
        public Int32 ReadBit()
        {
            if (_bitPosition / 8 >= _data.Length) return 0;
            var byteIndex = _bitPosition / 8;
            var bitIndex = 7 - (_bitPosition % 8);
            _bitPosition++;
            return (_data[byteIndex] >> bitIndex) & 1;
        }
        public Int32 ReadBits(Int32 count) { var v = 0; for (var i = 0; i < count; i++) v = (v << 1) | ReadBit(); return v; }
        public void SkipBits(Int32 count) => _bitPosition += count;
        public Int32 ReadUeGolomb()
        {
            var lz = 0; while (ReadBit() == 0 && lz < 32) lz++;
            if (lz >= 32) return 0;
            var v = 0; for (var i = 0; i < lz; i++) v = (v << 1) | ReadBit();
            return (1 << lz) - 1 + v;
        }
        public Int32 ReadSeGolomb() { var ue = ReadUeGolomb(); return (ue & 1) == 0 ? -(ue >> 1) : (ue + 1) >> 1; }
    }

    #endregion

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
        var buf = new Byte[8];
        var readSize = _stream.Read(buf, 0, (Int32)size);
        if (readSize < size) return 0;
        var reader = new SpanReader(buf) { IsLittleEndian = false };
        return size switch
        {
            1 => reader.ReadByte(),
            2 => reader.ReadUInt16(),
            3 => ((UInt64)reader.ReadByte() << 16) | ((UInt64)reader.ReadByte() << 8) | reader.ReadByte(),
            4 => reader.ReadUInt32(),
            5 => ((UInt64)reader.ReadByte() << 32) | reader.ReadUInt32(),
            6 => ((UInt64)reader.ReadUInt16() << 32) | reader.ReadUInt32(),
            7 => ((UInt64)reader.ReadByte() << 48) | ((UInt64)reader.ReadUInt16() << 32) | reader.ReadUInt32(),
            8 => reader.ReadUInt64(),
            _ => 0u,
        };
    }

    private Double ReadEbmlFloat(Int64 size)
    {
        if (size == 4)
        {
            var bytes = new Byte[4];
            _stream.Read(bytes, 0, 4);
            var reader = new SpanReader(bytes) { IsLittleEndian = false };
            return reader.ReadSingle();
        }
        if (size == 8)
        {
            var bytes = new Byte[8];
            _stream.Read(bytes, 0, 8);
            var reader = new SpanReader(bytes) { IsLittleEndian = false };
            return reader.ReadDouble();
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
        var buf = new Byte[2];
        _stream.Read(buf, 0, 2);
        var reader = new SpanReader(buf) { IsLittleEndian = false };
        return reader.ReadInt16();
    }

    private sealed class BlockEntry
    {
        public Byte[] Data;
        public Int64 PTS;
        public Boolean IsKeyFrame;
    }

    #endregion
}

