using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;
using NewLife.Video.Flv;
using NewLife.Video.Flv.Enums;
using NewLife.Video.Flv.Metadata;

namespace NewLife.Video.Flv;

/// <summary>FLV 容器写入器。将视频/音频编码数据封装为 FLV 文件，自动写入序列头和元数据</summary>
/// <remarks>
/// 首个关键帧数据中自动提取 SPS/PPS（H.264）或 VPS/SPS/PPS（H.265），构建解码器配置记录并写入序列头 Tag。
/// 支持 AAC 音频写入（通过 WriteAudioFrame）和 AMF3 onMetaData 元数据 Script Tag。
/// FLV 视频 Tag 数据格式：AVCPacketType(1) + CompositionTime(3) + Payload
/// </remarks>
public class FlvWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Boolean _headerWritten;
    private Boolean _scriptTagWritten;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private UInt32 _prevTagSize;

    private Boolean _sequenceHeaderWritten;
    private Byte[] _sps;
    private Byte[] _pps;
    private Byte[] _vps;

    private Boolean _audioHeaderWritten;
    private Int32 _sampleRate;
    private Int32 _channels;
    private Int32 _sampleBits;

    /// <summary>创建 FLV 写入器</summary>
    /// <param name="filePath">输出文件路径</param>
    public FlvWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    /// <summary>写入视频元数据</summary>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="codecType">编码类型</param>
    /// <param name="frameRate">帧率</param>
    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate;
        WriteFlvHeader();
        // 写入 AMF3 onMetaData Script Tag
        WriteScriptTag();
    }

    /// <summary>写入一帧视频数据。首个关键帧自动提取参数集并写入序列头</summary>
    /// <param name="packet">视频数据包</param>
    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;

        if (!_headerWritten) WriteFlvHeader();

        // TryGetArray 零拷贝：底层 ArrayPacket 且 offset=0 时直接复用，避免拷贝
        if (!packet.Data.TryGetArray(out var seg) || seg.Offset != 0 || seg.Count != seg.Array.Length)
            seg = new ArraySegment<Byte>(packet.Data.ToArray());

        // 首个关键帧：提取参数集并写入 Sequence Header Tag
        if (!_sequenceHeaderWritten && packet.IsKeyFrame)
        {
            ExtractParameterSets(seg.Array);
            WriteSequenceHeaderTag();
            _sequenceHeaderWritten = true;
        }

        // 写入视频帧 Tag（AVCPacketType=1 / HEVCPacketType=1）
        var codecId = _codecType == VideoTypes.H265 ? (Byte)12 : (Byte)7;
        WriteVideoTag(seg.Array, packet.PTS, packet.IsKeyFrame, codecId, packetType: 1);
    }

    /// <summary>写入音频元数据头（必须在写入音频帧之前调用）</summary>
    /// <param name="sampleRate">采样率（如 44100）</param>
    /// <param name="channels">声道数</param>
    /// <param name="sampleBits">采样位深</param>
    public void WriteAudioMetadata(Int32 sampleRate = 44100, Int32 channels = 2, Int32 sampleBits = 16)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _sampleBits = sampleBits;
    }

    /// <summary>写入音频帧数据（AAC 格式）</summary>
    /// <param name="data">AAC 编码数据（裸 AAC 或 ADTS 格式）</param>
    /// <param name="timestamp">时间戳（毫秒）</param>
    /// <param name="isSequenceHeader">是否是序列头（AudioSpecificConfig）</param>
    public void WriteAudioFrame(Byte[] data, Int64 timestamp, Boolean isSequenceHeader = false)
    {
        if (!_headerWritten) WriteFlvHeader();
        if (!_scriptTagWritten) WriteScriptTag();

        var ts = (UInt32)(timestamp > 0 ? timestamp : 0);

        // AAC 音频包类型
        var packetType = (Byte)(isSequenceHeader ? 0 : 1);

        // 构建 AacPacke: packetType(1) + rawData
        var audioPayload = new Byte[1 + data.Length];
        audioPayload[0] = packetType;
        if (data.Length > 0)
            Array.Copy(data, 0, audioPayload, 1, data.Length);

        // AudioTagHeader: SoundFormat(4) + SampleRate(2) + SampleBit(1) + Channel(1)
        var rateIdx = _sampleRate switch
        {
            5500 => 0,
            11025 => 1,
            22050 => 2,
            _ => 3, // 44100
        };
        var format = (Byte)AudioFormat.AAC;
        var sampleBit = _sampleBits >= 16 ? (Byte)SampleBit.Bit_16 : (Byte)SampleBit.Bit_8;
        var channel = _channels >= 2 ? (Byte)ChannelType.Stereo : (Byte)ChannelType.Mono;

        var audioHeader = (Byte)((format << 4) | (rateIdx << 2) | (sampleBit << 1) | channel);

        // Tag Header (11 bytes)
        var dataSize = 1 + audioPayload.Length; // audioHeader + payload
        var tagBuf = new Byte[11];
        var tw = new SpanWriter(tagBuf) { IsLittleEndian = false };
        tw.WriteByte((Byte)TagType.Audio); // audio tag
        tw.WriteByte((Byte)(dataSize >> 16));
        tw.WriteByte((Byte)(dataSize >> 8));
        tw.WriteByte((Byte)dataSize);
        tw.WriteByte((Byte)((ts >> 16) & 0xFF));
        tw.WriteByte((Byte)((ts >> 8) & 0xFF));
        tw.WriteByte((Byte)(ts & 0xFF));
        tw.WriteByte((Byte)((ts >> 24) & 0xFF));
        tw.WriteByte(0); // StreamID
        tw.WriteByte(0);
        tw.WriteByte(0);

        _stream.Write(tagBuf, 0, 11);
        _stream.WriteByte(audioHeader);
        _stream.Write(audioPayload, 0, audioPayload.Length);

        // PreviousTagSize
        var prevTagSize = (UInt32)(11 + dataSize);
        var ptsBuf = new Byte[4];
        var pw = new SpanWriter(ptsBuf) { IsLittleEndian = false };
        pw.Write(prevTagSize);
        _stream.Write(ptsBuf, 0, 4);
    }

    /// <summary>写入 AMF3 onMetaData Script Tag，包含视频和音频元数据信息</summary>
    public void WriteScriptTag()
    {
        if (_scriptTagWritten) return;

        // 使用 FlvMessagePackWriter 构建 Script Tag
        var bufferSize = Math.Max(1024, _width * _height / 100);
        var buffer = Flv.FlvArrayPool.Rent(bufferSize);
        try
        {
            var msgWriter = new Flv.MessagePack.FlvMessagePackWriter(buffer);
            var flvTags = new FlvTags
            {
                Type = TagType.ScriptData,
                DataTagsData = new Amf3
                {
                    DataType = 0x08, // AMF3 Array
                    Amf3Metadatas =
                    [
                        new Amf3Metadata_Duration { Value = 0.0 },
                        new Amf3Metadata_Width { Value = (Double)_width },
                        new Amf3Metadata_Height { Value = (Double)_height },
                        new Amf3Metadata_VideoCodecId { Value = (Double)(_codecType == VideoTypes.H265 ? 12 : 7) },
                        new Amf3Metadata_VideoDataRate { Value = 0.0 },
                        new Amf3Metadata_FrameRate { Value = _frameRate },
                    ],
                },
            };
            msgWriter.WriteFlvTag(flvTags);
            var tagData = msgWriter.FlushAndGetArray();

            var ts = 0u;
            var dataSize = tagData.Length - 11; // 减去 TagHeader 大小

            // 计算 TagHeader 中的 DataSize（TagHeader 之后的数据大小）
            // FlvMessagePackWriter 已经写入了完整的 Tag，我们直接写入文件
            _stream.Write(tagData, 0, tagData.Length);

            // 写入 PreviousTagSize（与 WriteVideoTag 保持一致）
            var prevTagSize = (UInt32)(tagData.Length);
            var ptsBuf = new Byte[4];
            var pw = new SpanWriter(ptsBuf) { IsLittleEndian = false };
            pw.Write(prevTagSize);
            _stream.Write(ptsBuf, 0, 4);

            _scriptTagWritten = true;
        }
        finally
        {
            Flv.FlvArrayPool.Return(buffer);
        }
    }

    /// <summary>刷新缓冲区</summary>
    public void Flush() => _stream.Flush();

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
        _stream?.Dispose();
    }
    private Boolean _disposed;

    #region 辅助

    /// <summary>从关键帧数据中提取 H.264 SPS/PPS 或 H.265 VPS/SPS/PPS</summary>
    private void ExtractParameterSets(Byte[] data)
    {
        if (_codecType == VideoTypes.H265)
            ExtractHevcParameterSets(data);
        else
            ExtractAvcParameterSets(data);
    }

    /// <summary>提取 H.264 SPS 和 PPS NAL 单元</summary>
    private void ExtractAvcParameterSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            // 查找 NAL 起始码 0x00000001 或 0x000001
            var startCodeLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                    startCodeLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1)
                    startCodeLen = 4;
            }

            if (startCodeLen == 0)
            {
                i++;
                continue;
            }

            var nalStart = i + startCodeLen;
            if (nalStart >= data.Length) break;

            var nalType = data[nalStart] & 0x1F;

            // 查找下一个起始码确定 NAL 长度
            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0)
                {
                    if (data[nalEnd + 2] == 1 ||
                        (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1))
                        break;
                }
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 7) // SPS
                _sps = nalData;
            else if (nalType == 8) // PPS
                _pps = nalData;

            if (_sps != null && _pps != null) break;

            i = nalEnd;
        }
    }

    /// <summary>提取 H.265 VPS、SPS 和 PPS NAL 单元</summary>
    private void ExtractHevcParameterSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            var startCodeLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                    startCodeLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1)
                    startCodeLen = 4;
            }

            if (startCodeLen == 0)
            {
                i++;
                continue;
            }

            var nalStart = i + startCodeLen;
            if (nalStart + 1 >= data.Length) break;

            // H.265 NAL 类型: (nal_header[0] >> 1) & 0x3F
            var nalType = (data[nalStart] >> 1) & 0x3F;

            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0)
                {
                    if (data[nalEnd + 2] == 1 ||
                        (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1))
                        break;
                }
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 32) // VPS
                _vps = nalData;
            else if (nalType == 33) // SPS
                _sps = nalData;
            else if (nalType == 34) // PPS
                _pps = nalData;

            if (_vps != null && _sps != null && _pps != null) break;

            i = nalEnd;
        }
    }

    /// <summary>写入序列头 Tag（AVCPacketType=0 或 HEVCPacketType=0）</summary>
    private void WriteSequenceHeaderTag()
    {
        Byte[] configRecord;
        var codecId = (Byte)7;

        if (_codecType == VideoTypes.H265)
        {
            configRecord = BuildHevcDecoderConfigurationRecord();
            codecId = 12;
        }
        else
        {
            configRecord = BuildAvcDecoderConfigurationRecord();
        }

        // Tag data: PacketType(1) + CompositionTime(3) + ConfigurationRecord
        var tagData = new Byte[4 + configRecord.Length];
        tagData[0] = 0; // AVCPacketType/HEVCPacketType = 0 (sequence header)
        tagData[1] = 0; // CompositionTime[0]
        tagData[2] = 0; // CompositionTime[1]
        tagData[3] = 0; // CompositionTime[2]
        Array.Copy(configRecord, 0, tagData, 4, configRecord.Length);

        WriteVideoTag(tagData, 0, true, codecId, packetType: 0);
    }

    /// <summary>构建 AVCDecoderConfigurationRecord（ISO 14496-15）</summary>
    private Byte[] BuildAvcDecoderConfigurationRecord()
    {
        var sps = _sps ?? new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0x40, 0x80 };
        var pps = _pps ?? new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };

        // AVCDecoderConfigurationRecord 结构
        using var ms = new MemoryStream();
        ms.WriteByte(1); // configurationVersion

        // profile_indication / profile_compatibility / level_indication 从 SPS 提取
        if (sps.Length > 3)
        {
            ms.WriteByte(sps[1]); // AVCProfileIndication
            ms.WriteByte(sps[2]); // profile_compatibility
            ms.WriteByte(sps[3]); // AVCLevelIndication
        }
        else
        {
            ms.WriteByte(0x42); // Baseline
            ms.WriteByte(0x00);
            ms.WriteByte(0x1E); // Level 3.0
        }

        // 6 bits reserved (111111) + 2 bits lengthSizeMinusOne (11 = 3, 即 NAL 长度字段 4 字节)
        ms.WriteByte(0xFF);

        // 3 bits reserved (111) + 5 bits numOfSequenceParameterSets
        ms.WriteByte((Byte)(0xE0 | 1)); // 1 个 SPS

        // SPS 长度 (2 字节大端) + SPS 数据
        var spsLen = (UInt16)sps.Length;
        ms.WriteByte((Byte)(spsLen >> 8));
        ms.WriteByte((Byte)(spsLen & 0xFF));
        ms.Write(sps, 0, sps.Length);

        // numOfPictureParameterSets
        ms.WriteByte(1); // 1 个 PPS

        // PPS 长度 (2 字节大端) + PPS 数据
        var ppsLen = (UInt16)pps.Length;
        ms.WriteByte((Byte)(ppsLen >> 8));
        ms.WriteByte((Byte)(ppsLen & 0xFF));
        ms.Write(pps, 0, pps.Length);

        return ms.ToArray();
    }

    /// <summary>构建 HEVCDecoderConfigurationRecord（ISO 14496-15）</summary>
    private Byte[] BuildHevcDecoderConfigurationRecord()
    {
        var vps = _vps ?? new Byte[] { 0x40, 0x01, 0x0C, 0x01, 0xFF, 0xFF, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9 };
        var sps = _sps ?? new Byte[] { 0x42, 0x01, 0x01, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9, 0xA0 };
        var pps = _pps ?? new Byte[] { 0x44, 0x01, 0xC0, 0x72, 0xB0, 0x46, 0x40 };

        using var ms = new MemoryStream();
        ms.WriteByte(1); // configurationVersion

        // profile_space(2) + tier_flag(1) + profile_idc(5) = SPS[1] & 0x1F, etc
        // general_profile_space: (sps[1] >> 6) & 0x03
        // general_tier_flag: (sps[1] >> 5) & 0x01
        // general_profile_idc: sps[1] & 0x1F
        var profileSpace = sps.Length > 1 ? (sps[1] >> 6) & 0x03 : 0;
        var tierFlag = sps.Length > 1 ? (sps[1] >> 5) & 0x01 : 0;
        var profileIdc = sps.Length > 1 ? sps[1] & 0x1F : 1;
        ms.WriteByte((Byte)((profileSpace << 6) | (tierFlag << 5) | profileIdc));

        // general_profile_compatibility_flags (4 bytes)
        ms.WriteByte(0x60); // bit 31..24
        ms.WriteByte(0x00); // bit 23..16
        ms.WriteByte(0x00); // bit 15..8
        ms.WriteByte(0x00); // bit 7..0

        // general_constraint_indicator_flags (6 bytes)
        for (var i = 0; i < 6; i++)
            ms.WriteByte(0x00);

        // general_level_idc
        var levelIdc = sps.Length > 3 ? sps[3] : (Byte)0x7A;
        ms.WriteByte(levelIdc);

        // min_spatial_segmentation_idc (4 bits reserved + 12 bits)
        ms.WriteByte(0xF0);
        ms.WriteByte(0x00);

        // parallelismType (6 bits reserved + 2 bits)
        ms.WriteByte(0xFC);

        // chromaFormatIdc (6 bits reserved + 2 bits)
        ms.WriteByte(0xFC);

        // bitDepthLumaMinus8 (5 bits reserved + 3 bits)
        ms.WriteByte(0xF8);

        // bitDepthChromaMinus8 (5 bits reserved + 3 bits)
        ms.WriteByte(0xF8);

        // avgFrameRate (2 bytes)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // constantFrameRate(2) + numTemporalLayers(3) + temporalIdNested(1) + lengthSizeMinusOne(2) = 1 字节
        ms.WriteByte(0x0F); // constant=0, numTemp=1, nested=1, lenSize=3

        // numOfArrays (8 bits)
        var arrayCount = (Byte)((_vps != null ? 1 : 0) + (_sps != null ? 1 : 0) + (_pps != null ? 1 : 0));
        ms.WriteByte(arrayCount);

        // 写入 VPS 数组
        if (_vps != null)
        {
            ms.WriteByte(32); // array_completeness(1) + reserved(1) + NAL_unit_type(6) = VPS=32
            ms.WriteByte(0x00);
            ms.WriteByte(1); // numNalus
            ms.WriteByte((Byte)(_vps.Length >> 8));
            ms.WriteByte((Byte)(_vps.Length & 0xFF));
            ms.Write(_vps, 0, _vps.Length);
        }

        // 写入 SPS 数组
        if (_sps != null)
        {
            ms.WriteByte(33); // SPS
            ms.WriteByte(0x00);
            ms.WriteByte(1);
            ms.WriteByte((Byte)(_sps.Length >> 8));
            ms.WriteByte((Byte)(_sps.Length & 0xFF));
            ms.Write(_sps, 0, _sps.Length);
        }

        // 写入 PPS 数组
        if (_pps != null)
        {
            ms.WriteByte(34); // PPS
            ms.WriteByte(0x00);
            ms.WriteByte(1);
            ms.WriteByte((Byte)(_pps.Length >> 8));
            ms.WriteByte((Byte)(_pps.Length & 0xFF));
            ms.Write(_pps, 0, _pps.Length);
        }

        return ms.ToArray();
    }

    /// <summary>写入视频 Tag（含 11 字节 TagHeader + Data + 4 字节 PreviousTagSize）</summary>
    private void WriteVideoTag(Byte[] data, Int64 pts, Boolean isKeyFrame, Byte codecId, Byte packetType)
    {
        var ts = (UInt32)(pts > 0 ? pts : 0);

        // Tag Data = VideoTagHeader(1) + payload
        var dataSize = 1 + data.Length;

        // FLV Tag Header: 11 bytes (大端)
        // [0] tagType(1) [1-3] dataSize(BE24) [4-6] timestamp(BE24) [7] tsExt(1) [8-10] streamId(3)
        var tagBuf = new Byte[11];
        var tw = new SpanWriter(tagBuf) { IsLittleEndian = false };
        tw.WriteByte(9); // video tag
        tw.WriteByte((Byte)(dataSize >> 16));
        tw.WriteByte((Byte)(dataSize >> 8));
        tw.WriteByte((Byte)dataSize);
        tw.WriteByte((Byte)((ts >> 16) & 0xFF));
        tw.WriteByte((Byte)((ts >> 8) & 0xFF));
        tw.WriteByte((Byte)(ts & 0xFF));
        tw.WriteByte((Byte)((ts >> 24) & 0xFF));
        tw.WriteByte(0); // StreamID[0]
        tw.WriteByte(0); // StreamID[1]
        tw.WriteByte(0); // StreamID[2]

        _stream.Write(tagBuf, 0, 11);

        // VideoTagHeader: 1 字节 FrameType(4bit) + CodecID(4bit)
        var frameType = isKeyFrame ? 1 : 2;
        _stream.WriteByte((Byte)((frameType << 4) | codecId));

        // 对于 AVC/HEVC，VideoTagHeader 后紧跟 AVCPacketType / HEVCPacketType（已包含在 data 中）
        // data 格式：[PacketType(1) + CompositionTime(3) + Payload]
        _stream.Write(data, 0, data.Length);

        // PreviousTagSize (4 bytes, big-endian) = 11(TagHeader) + dataSize
        var prevTagSize = (UInt32)(11 + dataSize);
        var ptsBuf = new Byte[4];
        var pw = new SpanWriter(ptsBuf) { IsLittleEndian = false };
        pw.Write(prevTagSize);
        _stream.Write(ptsBuf, 0, 4);
    }

    private void WriteFlvHeader()
    {
        // FLV Header: 9 bytes + First PreviousTagSize (4 bytes, 0)
        var buf = new Byte[13];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.WriteByte((Byte)'F');
        w.WriteByte((Byte)'L');
        w.WriteByte((Byte)'V');
        w.WriteByte(1);     // version
        w.WriteByte(0x01);  // flags: video only
        w.Write((Int32)9);    // header size = 9
        w.Write((Int32)0);    // First PreviousTagSize = 0

        _stream.Write(buf, 0, 13);
        _headerWritten = true;
    }

    #endregion
}
