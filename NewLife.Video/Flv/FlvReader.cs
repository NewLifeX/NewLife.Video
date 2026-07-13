using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.Flv;

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

        var reader = new SpanReader(header) { IsLittleEndian = false };
        reader.ReadBytes(4); // 跳过签名 + 版本
        var flags = reader.ReadByte();
        var hasVideo = (flags & 0x01) != 0;

        if (!hasVideo)
            throw new NotSupportedException("FLV 文件中没有视频轨");

        var dataOffset = reader.ReadUInt32();

        // 跳过到第一个标签
        _stream.Seek(dataOffset, SeekOrigin.Begin);
    }

    private void PreScanFrames()
    {
        // 快速扫描文件获取帧数、时长等元数据
        var fileLength = _stream.Length;
        var estimatedFrameSize = 0L;
        var frameCount = 0;

        try
        {
            var tagHeader = new Byte[11]; // 循环外分配，复用
            while (_stream.Position < fileLength - 11)
            {
                // 跳过 PreviousTagSize (4字节)
                _stream.Seek(4, SeekOrigin.Current);

                if (_stream.Read(tagHeader, 0, 11) < 11) break;
                var tagReader = new SpanReader(tagHeader) { IsLittleEndian = false };

                var tagType = tagReader.ReadByte();
                // DataSize: 3 字节大端（含 1 字节 VideoTagHeader）
                var dataSize = ((Int32)tagReader.ReadByte() << 16) | ((Int32)tagReader.ReadByte() << 8) | tagReader.ReadByte();
                // Timestamp: 3 字节大端 + 1 字节扩展
                var tsLow = ((UInt32)tagReader.ReadByte() << 16) | ((UInt32)tagReader.ReadByte() << 8) | tagReader.ReadByte();
                var tsHigh = (UInt32)tagReader.ReadByte();
                var timestamp = (Int64)(tsLow | (tsHigh << 24));
                // StreamID: 3 字节（tagReader 剩余 3 字节 = 0）

                // 读取 Tag Data 首字节获取 FrameType 和 CodecID
                var videoTagHeader = _stream.ReadByte();
                if (videoTagHeader < 0) break;
                var frameTypeAndCodec = (Byte)videoTagHeader;
                var payloadSize = dataSize - 1; // 减去 VideoTagHeader 字节

                if (tagType == 9) // 视频标签
                {
                    var frameType = (frameTypeAndCodec >> 4) & 0x0F;
                    var codecId = frameTypeAndCodec & 0x0F;

                    if (frameType == 1) // 关键帧
                    {
                        // 读取 AVCDecoderConfigurationRecord 或 HEVCDecoderConfigurationRecord
                        if (payloadSize > 4 && !_headerParsed)
                        {
                            var configBytes = new Byte[Math.Min(payloadSize, 256)];
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

                // 跳到下一个标签：跳过剩余 payload
                var bytesRead = Math.Min(payloadSize, 256);
                if (payloadSize > bytesRead)
                    _stream.Seek(payloadSize - bytesRead, SeekOrigin.Current);
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
        // data 格式：[AVCPacketType(1) + CompositionTime(3) + AVCDecoderConfigurationRecord...]
        // AVCDecoderConfigurationRecord 从 data[4] 开始
        var avccStart = 4;
        if (data.Length < avccStart + 6) return;

        // 验证 AVCPacketType 应为 0（Sequence Header）
        if (data[0] != 0) return;

        var configVersion = data[avccStart];
        if (configVersion != 1) return;

        // reserved(6) + lengthSizeMinusOne(2) = data[avccStart + 4]
        // reserved(3) + numOfSequenceParameterSets(5) = data[avccStart + 5]
        var numSps = data[avccStart + 5] & 0x1F;
        var offset = avccStart + 6; // SPS 长度字段开始位置

        for (var i = 0; i < numSps && offset + 2 <= data.Length; i++)
        {
            var reader = new SpanReader(data, offset) { IsLittleEndian = false };
            var spsLen = reader.ReadUInt16();
            offset += 2;

            if (offset + spsLen <= data.Length && spsLen >= 4)
            {
                ParseSps(data.AsSpan(offset, spsLen));
            }
            offset += spsLen;
        }
    }

    private void ParseSps(Span<Byte> sps)
    {
        // H.264 SPS 解析：提取宽高
        // 跳过 NAL header (1 byte) + profile_idc (1) + constraint_flags (1) + level_idc (1) = 4 bytes
        if (sps.Length < 4) return;

        try
        {
            // 从 bit 32 (4 bytes * 8) 开始读取 ue(v) 编码的值
            var bitReader = new BitReader(sps, 4 * 8);

            // seq_parameter_set_id: ue(v)
            bitReader.ReadUeGolomb();

            // 根据 profile 判断是否需要读取额外的 SPS 字段
            var profileIdc = sps[1];
            if (profileIdc == 100 || profileIdc == 110 || profileIdc == 122 || profileIdc == 244 ||
                profileIdc == 44 || profileIdc == 83 || profileIdc == 86 || profileIdc == 118 ||
                profileIdc == 128 || profileIdc == 138 || profileIdc == 139 || profileIdc == 134 || profileIdc == 135)
            {
                // chroma_format_idc: ue(v)
                var chromaFormatIdc = bitReader.ReadUeGolomb();
                if (chromaFormatIdc == 3)
                    bitReader.SkipBits(1); // separate_colour_plane_flag
                // bit_depth_luma_minus8: ue(v)
                bitReader.ReadUeGolomb();
                // bit_depth_chroma_minus8: ue(v)
                bitReader.ReadUeGolomb();
                // qpprime_y_zero_transform_bypass_flag: u(1)
                bitReader.SkipBits(1);
                // seq_scaling_matrix_present_flag: u(1)
                var scalingMatrixFlag = bitReader.ReadBit();
                if (scalingMatrixFlag != 0)
                {
                    var matrixCount = chromaFormatIdc != 3 ? 8 : 12;
                    for (var m = 0; m < matrixCount; m++)
                    {
                        var seqScalingListFlag = bitReader.ReadBit();
                        if (seqScalingListFlag != 0)
                        {
                            // 跳过缩放列表（简化处理，不解析具体值）
                            var sizeOfScalingList = m < 6 ? 16 : 64;
                            var lastScale = 8;
                            var nextScale = 8;
                            for (var j = 0; j < sizeOfScalingList; j++)
                            {
                                if (nextScale != 0)
                                {
                                    var deltaScale = bitReader.ReadSeGolomb();
                                    nextScale = (lastScale + deltaScale + 256) % 256;
                                }
                                lastScale = nextScale == 0 ? lastScale : nextScale;
                            }
                        }
                    }
                }
            }

            // log2_max_frame_num_minus4: ue(v)
            bitReader.ReadUeGolomb();
            // pic_order_cnt_type: ue(v)
            var picOrderCntType = bitReader.ReadUeGolomb();
            if (picOrderCntType == 0)
            {
                // log2_max_pic_order_cnt_lsb_minus4: ue(v)
                bitReader.ReadUeGolomb();
            }
            else if (picOrderCntType == 1)
            {
                bitReader.SkipBits(1); // delta_pic_order_always_zero_flag
                bitReader.ReadSeGolomb(); // offset_for_non_ref_pic
                bitReader.ReadSeGolomb(); // offset_for_top_to_bottom_field
                var numRefFramesInPicOrderCntCycle = bitReader.ReadUeGolomb();
                for (var r = 0; r < numRefFramesInPicOrderCntCycle; r++)
                    bitReader.ReadSeGolomb();
            }

            // max_num_ref_frames: ue(v)
            bitReader.ReadUeGolomb();
            // gaps_in_frame_num_value_allowed_flag: u(1)
            bitReader.SkipBits(1);
            // pic_width_in_mbs_minus1: ue(v)
            var picWidthInMbs = bitReader.ReadUeGolomb() + 1;
            // pic_height_in_map_units_minus1: ue(v)
            var picHeightInMapUnits = bitReader.ReadUeGolomb() + 1;

            _videoWidth = picWidthInMbs * 16;
            _videoHeight = picHeightInMapUnits * 16;

            // frame_mbs_only_flag: u(1) — 若为 0 则高度需乘 2
            var frameMbsOnlyFlag = bitReader.ReadBit();
            if (frameMbsOnlyFlag == 0)
                _videoHeight *= 2;
        }
        catch
        {
            // 解析失败，保持默认值
        }
    }

    /// <summary>读取下一帧</summary>
    public VPacket ReadFrame()
    {
        var tagHeader = new Byte[11]; // 循环外分配，复用
        while (_stream.Position < _stream.Length - 11)
        {
            // 跳过 PreviousTagSize
            _stream.Seek(4, SeekOrigin.Current);

            if (_stream.Read(tagHeader, 0, 11) < 11) return null;
            var tagReader = new SpanReader(tagHeader) { IsLittleEndian = false };

            var tagType = tagReader.ReadByte();
            // DataSize: 3 字节大端（含 1 字节 VideoTagHeader）
            var dataSize = ((Int32)tagReader.ReadByte() << 16) | ((Int32)tagReader.ReadByte() << 8) | tagReader.ReadByte();
            // Timestamp: 3 字节大端 + 1 字节扩展
            var tsLow = ((UInt32)tagReader.ReadByte() << 16) | ((UInt32)tagReader.ReadByte() << 8) | tagReader.ReadByte();
            var tsHigh = (UInt32)tagReader.ReadByte();
            var timestamp = (Int64)(tsLow | (tsHigh << 24));
            // StreamID: 3 字节（跳过，始终为 0）

            if (dataSize <= 0 || dataSize > _stream.Length - _stream.Position)
                return null;

            // 读取 Tag Data（含首字节 FrameType+CodecID）
            var videoData = new Byte[dataSize];
            var read = _stream.Read(videoData, 0, dataSize);
            if (read <= 1) return null;

            // FrameType+CodecID 在 Tag Data 首字节
            var frameTypeAndCodec = videoData[0];
            var frameType = (frameTypeAndCodec >> 4) & 0x0F;
            var codecId = frameTypeAndCodec & 0x0F;
            var payloadSize = read - 1;

            if (tagType == 9) // 视频标签
            {
                // 返回 payload（跳过首字节 FrameType+CodecID）
                var payload = new Byte[payloadSize];
                Array.Copy(videoData, 1, payload, 0, payloadSize);

                var packet = new VPacket
                {
                    Data = new ArrayPacket(payload),
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

            // 非视频标签已读完 dataSize 字节，继续循环
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

    #region 辅助

    /// <summary>位读取器，用于 H.264 SPS 指数哥伦布解码</summary>
    private ref struct BitReader
    {
        private readonly ReadOnlySpan<Byte> _data;
        private Int32 _bitPosition;

        public BitReader(ReadOnlySpan<Byte> data, Int32 startBit)
        {
            _data = data;
            _bitPosition = startBit;
        }

        /// <summary>读取 1 位</summary>
        public Int32 ReadBit()
        {
            if (_bitPosition / 8 >= _data.Length) return 0;
            var byteIndex = _bitPosition / 8;
            var bitIndex = 7 - (_bitPosition % 8);
            var bit = (_data[byteIndex] >> bitIndex) & 1;
            _bitPosition++;
            return bit;
        }

        /// <summary>跳过 n 位</summary>
        public void SkipBits(Int32 count) => _bitPosition += count;

        /// <summary>读取无符号指数哥伦布编码 ue(v)</summary>
        public Int32 ReadUeGolomb()
        {
            var leadingZeros = 0;
            while (ReadBit() == 0 && leadingZeros < 32)
                leadingZeros++;

            if (leadingZeros >= 32) return 0;

            var value = 0;
            for (var i = 0; i < leadingZeros; i++)
                value = (value << 1) | ReadBit();

            return (1 << leadingZeros) - 1 + value;
        }

        /// <summary>读取有符号指数哥伦布编码 se(v)</summary>
        public Int32 ReadSeGolomb()
        {
            var ue = ReadUeGolomb();
            if ((ue & 1) == 0)
                return -(ue >> 1);
            return (ue + 1) >> 1;
        }
    }

    #endregion
}
