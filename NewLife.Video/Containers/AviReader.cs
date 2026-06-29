using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>AVI (Audio Video Interleave) 容器读取器。基于 RIFF 格式解析</summary>
/// <remarks>
/// AVI 文件结构：RIFF('AVI ' → LIST('hdrl' → 'avih'/'strl') → LIST('movi') → 'idx1')
/// 支持未压缩视频和 MJPEG 编码视频。
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

            // 递归解析
            var fileSize = riff[4] | (riff[5] << 8) | (riff[6] << 16) | (riff[7] << 24);
            ParseChunks(12, Math.Min(12 + fileSize, _stream.Length));
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

            var id = System.Text.Encoding.ASCII.GetString(hdr, 0, 4).TrimEnd('\0');
            var size = hdr[4] | (hdr[5] << 8) | (hdr[6] << 16) | (hdr[7] << 24);
            if (size <= 0 || _stream.Position + size > _stream.Length) break;

            var dataStart = _stream.Position;
            var dataEnd = dataStart + size;

            if (id == "LIST")
            {
                var listType = new Byte[4];
                _stream.Read(listType, 0, 4);
                var type = System.Text.Encoding.ASCII.GetString(listType);

                if (type == "hdrl" || type == "strl")
                    ParseChunks(_stream.Position, dataEnd);
                else if (type == "movi")
                    _moviOffset = dataStart;
            }
            else if (id == "avih")
            {
                if (size >= 56)
                {
                    var avih = new Byte[56];
                    _stream.Position = dataStart;
                    _stream.Read(avih, 0, 56);
                    _totalFrames = avih[16] | (avih[17] << 8) | (avih[18] << 16) | (avih[19] << 24);
                    var scale = avih[20] | (avih[21] << 8) | (avih[22] << 16) | (avih[23] << 24);
                    var rate = avih[24] | (avih[25] << 8) | (avih[26] << 16) | (avih[27] << 24);
                    if (scale > 0) _frameRate = (Double)rate / scale;
                    _videoWidth = avih[32] | (avih[33] << 8) | (avih[34] << 16) | (avih[35] << 24);
                    _videoHeight = avih[36] | (avih[37] << 8) | (avih[38] << 16) | (avih[39] << 24);
                }
            }
            else if (id == "strh")
            {
                if (size >= 56)
                {
                    var strh = new Byte[56];
                    _stream.Position = dataStart;
                    _stream.Read(strh, 0, 56);
                    var fccType = System.Text.Encoding.ASCII.GetString(strh, 0, 4);
                    var fccHandler = System.Text.Encoding.ASCII.GetString(strh, 4, 4);
                    if (fccType == "vids")
                        _codecType = fccHandler == "MJPG" ? VideoTypes.MJPEG : fccHandler == "H264" ? VideoTypes.H264 : VideoTypes.Unknown;
                }
            }

            _stream.Seek(dataEnd, SeekOrigin.Begin);
        }
    }

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
            Data = new Packet(data, 0, read),
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

    public void Dispose() => _stream?.Dispose();
}
