using System.Text;
using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>AVI (Audio Video Interleave) 容器读取器。基于 RIFF 格式解析</summary>
/// <remarks>
/// AVI 文件结构：RIFF('AVI ' → LIST('hdrl' → 'avih'/'strl') → LIST('movi') → 'idx1')
/// 支持未压缩视频和 MJPEG/H.264 编码视频。
/// 优先使用 idx1 索引块获取帧偏移；无 idx1 时回退扫描 movi 块。
/// </remarks>
public class AviReader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private Int64 _frameIndex;
    private VideoTypes _codecType;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private Double _frameRate;
    private Int64 _totalFrames;
    private Int64 _duration;
    private Int64 _moviOffset;
    private Int64 _moviDataStart;
    private readonly List<Int64> _frameOffsets = [];
    private readonly List<Int32> _frameSizes = [];

    public VideoTypes VideoCodecType => _codecType;
    public Int32 VideoWidth => _videoWidth;
    public Int32 VideoHeight => _videoHeight;
    public Double FrameRate => _frameRate;
    public Int64 TotalFrames => _totalFrames;
    public Int64 Duration => _duration;

    public AviReader(String filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _totalFrames = -1;
        _duration = -1;
        _frameRate = 25.0;

        try
        {
            // 验证 RIFF 头
            var riff = new Byte[12];
            if (_stream.Read(riff, 0, 12) < 12) throw new InvalidOperationException("无效 AVI");
            if (riff[0] != 'R' || riff[1] != 'I' || riff[2] != 'F' || riff[3] != 'F')
                throw new InvalidOperationException("无效 RIFF");
            if (riff[8] != 'A' || riff[9] != 'V' || riff[10] != 'I' || riff[11] != ' ')
                throw new InvalidOperationException("不是 AVI");

            // 递归解析元数据（RIFF 小端）
            var riffReader = new SpanReader(riff, 4);
            var fileSize = riffReader.ReadUInt32();
            ParseChunks(12, Math.Min(12 + fileSize, _stream.Length));

            // 构建帧索引（idx1 块或扫描 movi）
            BuildFrameIndex();

            // 计算时长
            if (_totalFrames > 0 && _frameRate > 0)
                _duration = (Int64)(_totalFrames / _frameRate * 1000);
        }
        catch { _stream.Dispose(); throw; }
    }

    private void ParseChunks(Int64 start, Int64 end)
    {
        _stream.Seek(start, SeekOrigin.Begin);
        while (_stream.Position + 8 <= end)
        {
            var hdr = new Byte[8];
            if (_stream.Read(hdr, 0, 8) < 8) break;

            var id = Encoding.ASCII.GetString(hdr, 0, 4).TrimEnd('\0');
            var chunkReader = new SpanReader(hdr, 4); // 小端默认
            var size = chunkReader.ReadInt32();
            if (size <= 0 || _stream.Position + size > _stream.Length) break;

            var dataStart = _stream.Position;
            var dataEnd = dataStart + size;

            if (id == "LIST")
            {
                var listType = new Byte[4];
                _stream.Read(listType, 0, 4);
                var type = Encoding.ASCII.GetString(listType);

                if (type == "hdrl" || type == "strl")
                    ParseChunks(_stream.Position, dataEnd);
                else if (type == "movi")
                {
                    _moviOffset = start;
                    _moviDataStart = _stream.Position; // LIST type 之后是帧数据区
                }
            }
            else if (id == "avih")
            {
                if (size >= 56)
                {
                    var avih = new Byte[56];
                    _stream.Position = dataStart;
                    _stream.Read(avih, 0, 56);
                    var r = new SpanReader(avih);
                    _ = r.ReadBytes(16); // skip to dwTotalFrames
                    _totalFrames = r.ReadInt32();
                    var scale = r.ReadInt32();
                    var rate = r.ReadInt32();
                    if (scale > 0) _frameRate = (Double)rate / scale;
                    _ = r.ReadBytes(4); // skip dwSuggestedBufferSize to dwWidth
                    _videoWidth = r.ReadInt32();
                    _videoHeight = r.ReadInt32();
                }
            }
            else if (id == "strh")
            {
                if (size >= 56)
                {
                    var strh = new Byte[56];
                    _stream.Position = dataStart;
                    _stream.Read(strh, 0, 56);
                    var fccType = Encoding.ASCII.GetString(strh, 0, 4);
                    var fccHandler = Encoding.ASCII.GetString(strh, 4, 4);
                    if (fccType == "vids")
                        _codecType = fccHandler == "MJPG" ? VideoTypes.MJPEG : fccHandler == "H264" ? VideoTypes.H264 : VideoTypes.Unknown;
                }
            }

            _stream.Seek(dataEnd, SeekOrigin.Begin);
        }
    }

    #region 帧索引构建

    /// <summary>构建帧索引：优先 idx1 块，回退扫描 movi</summary>
    private void BuildFrameIndex()
    {
        if (TryParseIdx1())
            return;

        if (_moviDataStart > 0)
            ScanMoviBlocks();
    }

    /// <summary>尝试解析 idx1 索引块（旧式 AVI 索引）。每条记录 16 字节</summary>
    private Boolean TryParseIdx1()
    {
        var searchStart = Math.Max(0, _stream.Length - 1024 * 1024);
        _stream.Seek(searchStart, SeekOrigin.Begin);

        var buf = new Byte[8];
        while (_stream.Position + 16 <= _stream.Length)
        {
            var pos = _stream.Position;
            if (_stream.Read(buf, 0, 8) < 8) break;

            var id = Encoding.ASCII.GetString(buf, 0, 4);
            var chunkReader = new SpanReader(buf, 4);
            var size = chunkReader.ReadInt32();

            if (id == "idx1" && size > 0 && size % 16 == 0 && pos + 8 + size <= _stream.Length)
            {
                var entryCount = size / 16;
                var idx1Data = new Byte[size];
                _stream.Read(idx1Data, 0, size);

                for (var i = 0; i < entryCount; i++)
                {
                    var off = i * 16;
                    var reader = new SpanReader(idx1Data, off);
                    var chunkId = reader.ReadUInt32();
                    var flags = reader.ReadUInt32();
                    var chunkOffset = reader.ReadUInt32();
                    var chunkSize = reader.ReadUInt32();

                    var c1 = (Char)(chunkId & 0xFF);
                    var c2 = (Char)((chunkId >> 8) & 0xFF);
                    if ((c1 == 'd' && c2 == 'c') || (c1 == 'd' && c2 == 'b'))
                    {
                        _frameOffsets.Add(_moviDataStart + chunkOffset);
                        _frameSizes.Add((Int32)chunkSize);
                    }
                }

                if (_frameOffsets.Count > 0)
                {
                    if (_totalFrames <= 0) _totalFrames = _frameOffsets.Count;
                    return true;
                }
            }

            _stream.Seek(pos + 1, SeekOrigin.Begin);
        }

        return false;
    }

    /// <summary>扫描 movi 数据块获取帧偏移和大小（无 idx1 时的回退方案）</summary>
    private void ScanMoviBlocks()
    {
        _stream.Seek(_moviDataStart, SeekOrigin.Begin);
        var fileEnd = _stream.Length;
        var buf = new Byte[8];

        while (_stream.Position + 8 <= fileEnd)
        {
            var pos = _stream.Position;
            if (_stream.Read(buf, 0, 8) < 8) break;

            var id = Encoding.ASCII.GetString(buf, 0, 4);
            var chunkReader = new SpanReader(buf, 4);
            var size = chunkReader.ReadInt32();

            if (size <= 0 || pos + 8 + size > fileEnd) break;

            if ((id == "00dc" || id == "00db") && size > 0)
            {
                _frameOffsets.Add(pos + 8);
                _frameSizes.Add(size);
            }

            var alignedSize = (size + 1) & ~1;
            _stream.Seek(pos + 8 + alignedSize, SeekOrigin.Begin);
        }

        if (_totalFrames <= 0) _totalFrames = _frameOffsets.Count;
    }

    #endregion

    public VPacket ReadFrame()
    {
        if (_frameIndex >= _frameOffsets.Count) return null;
        var offset = _frameOffsets[(Int32)_frameIndex];
        var size = _frameSizes[(Int32)_frameIndex];

        _stream.Seek(offset, SeekOrigin.Begin);
        var data = new Byte[size];
        var read = _stream.Read(data, 0, size);
        if (read <= 0) return null;

        var packet = new VPacket
        {
            Data = new ArrayPacket(data, 0, read),
            CodecType = _codecType,
            PTS = (Int64)(_frameIndex / _frameRate * 1000),
            FrameIndex = _frameIndex,
            IsKeyFrame = _frameIndex == 0,
        };
        _frameIndex++;
        return packet;
    }

    public Boolean SeekFrame(Int64 frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frameOffsets.Count) return false;
        _frameIndex = frameIndex;
        return true;
    }

    public void Dispose() { if (_disposed) return; _disposed = true; _stream?.Dispose(); }
    private Boolean _disposed;
}
