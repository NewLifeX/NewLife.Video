using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>FLV (Flash Video) 容器读取器。纯托管解析 FLV 文件头和标签结构</summary>
/// <remarks>
/// FLV 文件结构：FLV Header (9字节) → PreviousTagSize (4字节) → Tag1 → PreviousTagSize → Tag2 → ...
/// 每个 Tag：TagHeader (11字节) + TagData
/// 支持 H.264 (AVC) 和 H.265 (HEVC) 视频标签。
/// </remarks>
public class FlvReader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private Int64 _frameIndex;
    private Boolean _headerParsed;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private Double _frameRate;
    private VideoTypes _codecType;
    private Int64 _totalFrames;
    private Int64 _duration;
    private readonly List<Double> _frameTimestamps = [];

    /// <summary>视频编码类型</summary>
    public VideoTypes VideoCodecType => _codecType;

    /// <summary>视频宽度</summary>
    public Int32 VideoWidth => _videoWidth;

    /// <summary>视频高度</summary>
    public Int32 VideoHeight => _videoHeight;

    /// <summary>帧率</summary>
    public Double FrameRate => _frameRate;

    /// <summary>总帧数（预先计算或估算）</summary>
    public Int64 TotalFrames => _totalFrames;

    /// <summary>时长（毫秒）</summary>
    public Int64 Duration => _duration;

    /// <summary>创建 FLV 读取器</summary>
    /// <param name="filePath">FLV 文件路径</param>
    public FlvReader(String filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);

        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _totalFrames = -1;
        _duration = -1;
        _frameRate = 25.0;

        try
        {
            ParseFlvHeader();
            PreScanFrames();
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    private void ParseFlvHeader()
    {
        var header = new Byte[9];
        if (_stream.Read(header, 0, 9) < 9)
            throw new InvalidOperationException("无效的 FLV 文件：文件头不足 9 字节");

        // 验证 FLV 签名
        if (header[0] != 'F' || header[1] != 'L' || header[2] != 'V')
            throw new InvalidOperationException("不是有效的 FLV 文件");

        var flags = header[4];
        var hasVideo = (flags & 0x01) != 0;
        var hasAudio = (flags & 0x04) != 0;

        if (!hasVideo)
            throw new NotSupportedException("FLV 文件中没有视频轨");

        var dataOffset = (header[5] << 24) | (header[6] << 16) | (header[7] << 8) | header[8];

        // 跳过到第一个标签
        _stream.Seek(dataOffset, SeekOrigin.Begin);
    }

    private void PreScanFrames()
    {
        // 快速扫描文件获取帧数、时长等元数据
        var startPos = _stream.Position;
        var fileLength = _stream.Length;
        var estimatedFrameSize = 0L;
        var frameCount = 0;

        try
        {
            while (_stream.Position < fileLength - 11)
            {
                // 跳过 PreviousTagSize (4字节)
                _stream.Seek(4, SeekOrigin.Current);

                var tagHeader = new Byte[11];
                if (_stream.Read(tagHeader, 0, 11) < 11) break;

                var tagType = tagHeader[0];
                var dataSize = (tagHeader[1] << 16) | (tagHeader[2] << 8) | tagHeader[3];
                var timestamp = (tagHeader[4] << 16) | (tagHeader[5] << 8) | tagHeader[6] | (tagHeader[7] << 24);

                if (tagType == 9) // 视频标签
                {
                    var frameType = (tagHeader[8] >> 4) & 0x0F; // 前 4 位为帧类型
                    var codecId = tagHeader[8] & 0x0F;           // 后 4 位为编码 ID

                    if (frameType == 1) // 关键帧
                    {
                        // 读取 AVCDecoderConfigurationRecord 或 HEVCDecoderConfigurationRecord
                        if (dataSize > 5 && !_headerParsed)
                        {
                            var configBytes = new Byte[Math.Min(dataSize, 256)];
                            _stream.Read(configBytes, 0, configBytes.Length);

                            if (codecId == 7) // AVC (H.264)
                            {
                                _codecType = VideoTypes.H264;
                                ParseAvcConfig(configBytes);
                            }
                            else if (codecId == 12) // HEVC (H.265)
                            {
                                _codecType = VideoTypes.H265;
                            }

                            _headerParsed = true;
                        }
                    }

                    _frameTimestamps.Add(timestamp);
                    frameCount++;
                    estimatedFrameSize += dataSize;
                }

                // 跳到下一个标签
                _stream.Seek(dataSize - (_stream.Position - (startPos + 4 + 11)) + tagHeader.Length - 11, SeekOrigin.Current);
                _stream.Seek(4, SeekOrigin.Current); // 跳过 PreviousTagSize
            }
        }
        catch
        {
            // 预扫描出错不抛异常，使用已收集的信息
        }

        _totalFrames = frameCount;
        if (frameCount > 1)
        {
            var totalTime = _frameTimestamps[^1] - _frameTimestamps[0];
            if (totalTime > 0)
            {
                _frameRate = (frameCount - 1) / (totalTime / 1000.0);
                _duration = (Int64)totalTime;
            }
        }

        if (estimatedFrameSize > 0 && frameCount > 0)
        {
            var avgFrameSize = estimatedFrameSize / frameCount;
            _totalFrames = fileLength / avgFrameSize;
        }

        // 回到数据区起始位置，准备读取
        var headerLen = 9;
        _stream.Seek(headerLen + 4, SeekOrigin.Begin); // 跳过 FLV Header (9) + First PreviousTagSize (4)
    }

    private void ParseAvcConfig(Byte[] data)
    {
        // AVC Decoder Configuration Record (ISO 14496-15)
        // 跳过 AVCPacketType (1) + CompositionTime (3) = 4 字节
        if (data.Length < 10) return;

        var configVersion = data[4 + 5]; // offset: 4(PacketType+CT) + 5(configVersion位置)
        if (configVersion != 1) return;

        // SPS 数量
        var numSps = data[4 + 7] & 0x1F;
        var offset = 4 + 8;
        for (var i = 0; i < numSps && offset + 2 < data.Length; i++)
        {
            var spsLen = (data[offset] << 8) | data[offset + 1];
            offset += 2;

            if (offset + spsLen <= data.Length && spsLen > 4)
            {
                ParseSps(data.AsSpan(offset, spsLen));
            }
            offset += spsLen;
        }
    }

    private void ParseSps(Span<Byte> sps)
    {
        // 简单 SPS 解析：提取宽高
        // profile_idc (8) + constraints + level_idc (8) = 跳过 3 字节
        if (sps.Length < 7) return;

        var profileIdc = sps[1];
        // 跳过直到 pic_width_in_luma_samples 和 pic_height_in_luma_samples
        // 简化：从 H.264 SPS 中解析宽高（使用指数哥伦布编码解码）
        try
        {
            var width = ParseUeGolomb(sps, 3 * 8) * 16;
            var height = ParseUeGolomb(sps, -1) * 16; // 继续从当前位置读取
            if (width > 0 && height > 0)
            {
                _videoWidth = width;
                _videoHeight = height;
            }
        }
        catch
        {
            // 解析失败，保持默认值
        }
    }

    private static Int32 ParseUeGolomb(Span<Byte> data, Int32 bitOffset)
    {
        // 简化的指数哥伦布解码（ue(v)）
        // 此为实现骨架，后续完善
        return 0;
    }

    /// <summary>读取下一帧</summary>
    public VPacket ReadFrame()
    {
        while (_stream.Position < _stream.Length - 11)
        {
            // 跳过 PreviousTagSize
            _stream.Seek(4, SeekOrigin.Current);

            var tagHeader = new Byte[11];
            if (_stream.Read(tagHeader, 0, 11) < 11) return null;

            var tagType = tagHeader[0];
            var dataSize = (tagHeader[1] << 16) | (tagHeader[2] << 8) | tagHeader[3];
            var timestamp = (tagHeader[4] << 16) | (tagHeader[5] << 8) | tagHeader[6] | (tagHeader[7] << 24);

            if (dataSize <= 0 || dataSize > _stream.Length - _stream.Position)
                return null;

            if (tagType == 9) // 视频标签
            {
                var frameType = (tagHeader[8] >> 4) & 0x0F;
                var codecId = tagHeader[8] & 0x0F;

                var videoData = new Byte[dataSize];
                var read = _stream.Read(videoData, 0, dataSize);
                if (read <= 0) return null;

                var packet = new VPacket
                {
                    Data = new Packet(videoData, 0, read),
                    CodecType = codecId == 7 ? VideoTypes.H264 :
                                codecId == 12 ? VideoTypes.H265 :
                                codecId == 4 ? VideoTypes.H264 : // Screen Video 等
                                VideoTypes.Unknown,
                    PTS = timestamp,
                    DTS = timestamp,
                    IsKeyFrame = frameType == 1,
                    FrameIndex = _frameIndex,
                };

                _frameIndex++;
                return packet;
            }

            // 跳过非视频标签数据
            _stream.Seek(dataSize, SeekOrigin.Current);
        }

        return null;
    }

    /// <summary>跳转到指定帧</summary>
    public Boolean SeekFrame(Int64 frameIndex)
    {
        if (frameIndex < 0) return false;

        // FLV 不支持随机访问，顺序跳到目标帧
        _stream.Seek(0, SeekOrigin.Begin);
        ParseFlvHeader();
        _frameIndex = 0;

        VPacket packet;
        while ((packet = ReadFrame()) != null)
        {
            if (_frameIndex - 1 >= frameIndex) return true;
        }

        return false;
    }

    /// <summary>释放资源</summary>
    public void Dispose() => _stream?.Dispose();
}
