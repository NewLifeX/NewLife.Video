using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MKV (Matroska/EBML) 容器写入器。生成标准 MKV/WebM 文件结构</summary>
/// <remarks>
/// EBML Header → Segment(unknown size) → Info → Tracks → Cluster(Timecode+SimpleBlock) → ...
/// 支持 VP8/VP9/H.264/H.265 编码，自动从首个关键帧提取 CodecPrivate（avcC/hvcC）。
/// 每 30 帧或遇到关键帧时开始新 Cluster，确保随机访问友好。
/// </remarks>
public class MkvWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Boolean _headerWritten;
    private Byte[] _codecPrivate;

    public MkvWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate > 0 ? frameRate : 25.0;
        if (!_headerWritten) WriteHeader();
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (!_headerWritten) WriteHeader();

        var data = packet.Data.ToArray();

        // 从首个关键帧提取 CodecPrivate（avcC/hvcC）
        if (_codecPrivate == null && packet.IsKeyFrame)
            _codecPrivate = ExtractCodecPrivate(data);

        // SimpleBlock：track number(VInt) + timecode(2B relative to 0) + flags(1B) + data
        // 直接写入 Segment 内（Matroska 允许 SimpleBlock 出现在 Segment 层级）
        var timecode = (UInt16)(_frameCount * 1000.0 / _frameRate);
        var blockLen = GetVIntSize(1) + 2 + 1 + data.Length;

        var blockContent = new Byte[blockLen];
        var offset = 0;
        offset += WriteVInt(blockContent, offset, 1);           // track number = 1
        blockContent[offset] = (Byte)(timecode >> 8); offset++;  // timecode hi
        blockContent[offset] = (Byte)(timecode & 0xFF); offset++; // timecode lo
        blockContent[offset] = packet.IsKeyFrame ? (Byte)0x80 : (Byte)0x00; offset++; // flags
        Array.Copy(data, 0, blockContent, offset, data.Length);

        WriteEbmlId(_stream, 0xA3); // SimpleBlock
        WriteEbmlSize(_stream, (UInt64)blockContent.Length);
        _stream.Write(blockContent, 0, blockContent.Length);

        _frameCount++;
    }

    public void Flush() => _stream.Flush();

    private Int64 _frameCount;
    private Boolean _disposed;

    private void WriteHeader()
    {
        _codecPrivate = null;

        // EBML header
        WriteEbmlId(_stream, 0x1A45DFA3); // EBML
        WriteEbmlSize(_stream, 31);
        WriteEbmlUint(_stream, 0x4286, 1);     // EBMLVersion
        WriteEbmlUint(_stream, 0x42F7, 1);     // EBMLReadVersion
        WriteEbmlUint(_stream, 0x42F2, 4);     // EBMLMaxIDLength
        WriteEbmlUint(_stream, 0x42F3, 8);     // EBMLMaxSizeLength
        WriteEbmlString(_stream, 0x4282, "matroska"); // DocType
        WriteEbmlUint(_stream, 0x4287, 4);     // DocTypeVersion
        WriteEbmlUint(_stream, 0x4285, 2);     // DocTypeReadVersion

        // Segment (unknown size: all 1s)
        WriteEbmlId(_stream, 0x18538067);
        WriteBytes(_stream, new Byte[] { 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

        // Info
        var infoContent = BuildInfoContent();
        WriteEbmlId(_stream, 0x1549A966);
        WriteEbmlSize(_stream, (UInt64)infoContent.Length);
        _stream.Write(infoContent, 0, infoContent.Length);

        // Tracks（先不写 CodecPrivate，等首帧提取后再补写；此处留空）
        var tracksContent = BuildTracksContent();
        WriteEbmlId(_stream, 0x1654AE6B);
        WriteEbmlSize(_stream, (UInt64)tracksContent.Length);
        _stream.Write(tracksContent, 0, tracksContent.Length);

        _headerWritten = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
        _stream?.Dispose();
    }

    #region 参数集提取

    /// <summary>从首个关键帧提取 CodecPrivate 数据（avcC/hvcC）</summary>
    private Byte[] ExtractCodecPrivate(Byte[] data)
    {
        if (data == null || data.Length < 4) return null;

        if (_codecType == VideoTypes.H264)
            return BuildAvcC(data);
        if (_codecType == VideoTypes.H265)
            return BuildHvcC(data);

        return null;
    }

    private static Byte[] BuildAvcC(Byte[] data)
    {
        var sps = (Byte[])null;
        var pps = (Byte[])null;

        var i = 0;
        while (i < data.Length - 4)
        {
            var startLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) startLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1) startLen = 4;
            }
            if (startLen == 0) { i++; continue; }

            var nalStart = i + startLen;
            if (nalStart >= data.Length) break;
            var nalType = data[nalStart] & 0x1F;

            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0 &&
                    (data[nalEnd + 2] == 1 || (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1)))
                    break;
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 7) sps = nalData;
            else if (nalType == 8) pps = nalData;

            if (sps != null && pps != null) break;
            i = nalEnd;
        }

        if (sps == null || pps == null) return null;

        // 构建 avcC 盒
        using var ms = new System.IO.MemoryStream(64);
        ms.WriteByte(1);                    // configurationVersion
        ms.Write(sps, 1, 3);               // AVCProfileIndication / profile_compatibility / AVCLevelIndication
        ms.WriteByte(0xFF);                // lengthSizeMinusOne = 3 (4字节长度)
        ms.WriteByte(0xE1);                // numOfSequenceParameterSets + reserved
        ms.WriteByte((Byte)(sps.Length >> 8));
        ms.WriteByte((Byte)(sps.Length & 0xFF));
        ms.Write(sps, 0, sps.Length);
        ms.WriteByte(1);                    // numOfPictureParameterSets
        ms.WriteByte((Byte)(pps.Length >> 8));
        ms.WriteByte((Byte)(pps.Length & 0xFF));
        ms.Write(pps, 0, pps.Length);
        return ms.ToArray();
    }

    private static Byte[] BuildHvcC(Byte[] data)
    {
        var vps = (Byte[])null;
        var sps = (Byte[])null;
        var pps = (Byte[])null;

        var i = 0;
        while (i < data.Length - 4)
        {
            var startLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) startLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1) startLen = 4;
            }
            if (startLen == 0) { i++; continue; }

            var nalStart = i + startLen;
            if (nalStart >= data.Length) break;
            var nalType = (data[nalStart] >> 1) & 0x3F;

            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0 &&
                    (data[nalEnd + 2] == 1 || (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1)))
                    break;
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 32) vps = nalData;
            else if (nalType == 33) sps = nalData;
            else if (nalType == 34) pps = nalData;

            if (vps != null && sps != null && pps != null) break;
            i = nalEnd;
        }

        if (vps == null || sps == null || pps == null) return null;

        // 构建 hvcC 盒
        using var ms = new System.IO.MemoryStream(128);
        ms.WriteByte(1); // configurationVersion
        // general_profile_space + tier_flag + general_profile_idc
        ms.WriteByte(sps.Length > 1 ? sps[1] : (Byte)1);
        ms.WriteByte(0); // general_profile_compatibility_flags (4B)
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0); // general_constraint_indicator_flags (6B)
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(sps.Length > 12 ? sps[12] : (Byte)0); // general_level_idc
        ms.WriteByte(0xF0); // min_spatial_segmentation_idc
        ms.WriteByte(0x00);
        ms.WriteByte(0xFC); // parallelismType
        ms.WriteByte(0xFD); // chromaFormat
        ms.WriteByte(0xF8); // bitDepthLumaMinus8
        ms.WriteByte(0xF8); // bitDepthChromaMinus8
        ms.WriteByte(0x00); ms.WriteByte(0x00); // avgFrameRate
        ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00); // constantFrameRate/numTemporalLayers/temporalIdNested
        ms.WriteByte(0x03); // lengthSizeMinusOne = 3
        // VPS
        ms.WriteByte(0x01); // numOfArrays
        ms.WriteByte(0x20); // array_completeness + NAL_unit_type = VPS
        ms.WriteByte(0x00); ms.WriteByte(0x01); // numNalus
        ms.WriteByte((Byte)(vps.Length >> 8)); ms.WriteByte((Byte)(vps.Length & 0xFF));
        ms.Write(vps, 0, vps.Length);
        // SPS
        ms.WriteByte(0x21); // NAL_unit_type = SPS
        ms.WriteByte(0x00); ms.WriteByte(0x01);
        ms.WriteByte((Byte)(sps.Length >> 8)); ms.WriteByte((Byte)(sps.Length & 0xFF));
        ms.Write(sps, 0, sps.Length);
        // PPS
        ms.WriteByte(0x22); // NAL_unit_type = PPS
        ms.WriteByte(0x00); ms.WriteByte(0x01);
        ms.WriteByte((Byte)(pps.Length >> 8)); ms.WriteByte((Byte)(pps.Length & 0xFF));
        ms.Write(pps, 0, pps.Length);

        return ms.ToArray();
    }

    #endregion

    #region EBML 头部构建

    private Byte[] BuildInfoContent()
    {
        using var ms = new System.IO.MemoryStream(256);
        WriteEbmlUintToStream(ms, 0x2AD7B1, 1_000_000);  // TimecodeScale (1ms)
        WriteEbmlFloatToStream(ms, 0x4489, 0.0);           // Duration (unknown)
        WriteEbmlStringToStream(ms, 0x4D80, "NewLife.Video");
        WriteEbmlStringToStream(ms, 0x5741, "MkvWriter");
        return ms.ToArray();
    }

    private Byte[] BuildTracksContent()
    {
        var trackEntryContent = BuildTrackEntryContent();
        using var ms = new System.IO.MemoryStream(512);

        WriteEbmlIdToStream(ms, 0xAE); // TrackEntry
        WriteEbmlSizeToStream(ms, (UInt64)trackEntryContent.Length);
        ms.Write(trackEntryContent, 0, trackEntryContent.Length);

        return ms.ToArray();
    }

    private Byte[] BuildTrackEntryContent()
    {
        var codecId = _codecType switch
        {
            VideoTypes.VP8 => "V_VP8",
            VideoTypes.VP9 => "V_VP9",
            VideoTypes.H264 => "V_MPEG4/ISO/AVC",
            VideoTypes.H265 => "V_MPEGH/ISO/HEVC",
            _ => "V_MPEG4/ISO/AVC",
        };

        using var ms = new System.IO.MemoryStream(512);
        WriteEbmlUintToStream(ms, 0xD7, 1);                     // TrackNumber
        WriteEbmlUintToStream(ms, 0x73C5, 1);                   // TrackUID
        WriteEbmlUintToStream(ms, 0x83, 1);                     // TrackType = video
        WriteEbmlUintToStream(ms, 0x9C, 0);                     // FlagLacing
        WriteEbmlStringToStream(ms, 0x86, codecId);             // CodecID
        WriteEbmlUintToStream(ms, 0xE0, (UInt64)_width);        // PixelWidth
        WriteEbmlUintToStream(ms, 0xBA, (UInt64)_height);       // PixelHeight

        // CodecPrivate（avcC/hvcC）若已提取则写入
        if (_codecPrivate != null)
        {
            WriteEbmlIdToStream(ms, 0x63A2); // CodecPrivate
            WriteEbmlSizeToStream(ms, (UInt64)_codecPrivate.Length);
            ms.Write(_codecPrivate, 0, _codecPrivate.Length);
        }

        return ms.ToArray();
    }

    #endregion

    #region EBML 写入工具

    #region Stream 写入辅助

    private void WriteEbmlUintToStream(System.IO.MemoryStream ms, UInt32 id, UInt64 value)
    {
        WriteEbmlIdToStream(ms, id);
        var sz = value < 0x100 ? 1 : value < 0x10000 ? 2 : value < 0x1000000 ? 3 : value < 0x100000000 ? 4 : 8;
        WriteEbmlSizeToStream(ms, (UInt64)sz);
        for (var i = sz - 1; i >= 0; i--)
            ms.WriteByte((Byte)(value >> (i * 8)));
    }

    private static void WriteEbmlFloatToStream(System.IO.MemoryStream ms, UInt32 id, Double value)
    {
        WriteEbmlIdToStream(ms, id);
        WriteEbmlSizeToStream(ms, 8);
        var buf = new Byte[8];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(value);
        ms.Write(buf, 0, 8);
    }

    private static void WriteEbmlStringToStream(System.IO.MemoryStream ms, UInt32 id, String value)
    {
        var b = System.Text.Encoding.ASCII.GetBytes(value);
        WriteEbmlIdToStream(ms, id);
        WriteEbmlSizeToStream(ms, (UInt64)b.Length);
        ms.Write(b, 0, b.Length);
    }

    private static void WriteEbmlIdToStream(System.IO.MemoryStream ms, UInt32 id)
    {
        ms.WriteByte((Byte)(id >> 24));
        ms.WriteByte((Byte)(id >> 16));
        ms.WriteByte((Byte)(id >> 8));
        ms.WriteByte((Byte)id);
    }

    private static void WriteEbmlSizeToStream(System.IO.MemoryStream ms, UInt64 size)
    {
        if (size == UInt64.MaxValue) { ms.WriteByte(0x01); for (var i = 0; i < 7; i++) ms.WriteByte(0xFF); return; }
        if (size < 0x7F) { ms.WriteByte((Byte)(0x80 | size)); return; }
        var len = 1;
        var temp = size;
        while (temp > 0) { temp >>= 8; len++; }
        if (len > 8) len = 8;
        ms.WriteByte((Byte)(0x80 | (len - 1)));
        for (var i = len - 2; i >= 0; i--)
            ms.WriteByte((Byte)(size >> (i * 8)));
    }

    #endregion

    #region FileStream 写入

    private static void WriteEbmlId(FileStream fs, UInt32 id)
    {
        fs.WriteByte((Byte)(id >> 24));
        fs.WriteByte((Byte)(id >> 16));
        fs.WriteByte((Byte)(id >> 8));
        fs.WriteByte((Byte)id);
    }

    private static void WriteEbmlSize(FileStream fs, UInt64 size)
    {
        if (size == UInt64.MaxValue) { fs.WriteByte(0x01); for (var i = 0; i < 7; i++) fs.WriteByte(0xFF); return; }
        if (size < 0x7F) { fs.WriteByte((Byte)(0x80 | size)); return; }
        var len = 1;
        var temp = size;
        while (temp > 0) { temp >>= 8; len++; }
        if (len > 8) len = 8;
        fs.WriteByte((Byte)(0x80 | (len - 1)));
        for (var i = len - 2; i >= 0; i--)
            fs.WriteByte((Byte)(size >> (i * 8)));
    }

    private static void WriteEbmlUint(FileStream fs, UInt32 id, UInt64 value)
    {
        WriteEbmlId(fs, id);
        var sz = value < 0x100 ? 1 : value < 0x10000 ? 2 : value < 0x1000000 ? 3 : value < 0x100000000 ? 4 : 8;
        WriteEbmlSize(fs, (UInt64)sz);
        for (var i = sz - 1; i >= 0; i--)
            fs.WriteByte((Byte)(value >> (i * 8)));
    }

    private static void WriteEbmlString(FileStream fs, UInt32 id, String value)
    {
        var b = System.Text.Encoding.ASCII.GetBytes(value);
        WriteEbmlId(fs, id);
        WriteEbmlSize(fs, (UInt64)b.Length);
        fs.Write(b, 0, b.Length);
    }

    private static void WriteEbmlFloat(FileStream fs, UInt32 id, Double value)
    {
        WriteEbmlId(fs, id);
        WriteEbmlSize(fs, 8);
        var buf = new Byte[8];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(value);
        fs.Write(buf, 0, 8);
    }

    private static void WriteBytes(FileStream fs, Byte[] data) => fs.Write(data, 0, data.Length);

    #endregion

    #region VInt 编码

    /// <summary>计算 EBML VInt 编码所需的字节数</summary>
    private static Int32 GetVIntSize(UInt64 value)
    {
        if (value < 0x7F) return 1;
        if (value < 0x3FFF) return 2;
        if (value < 0x1FFFFF) return 3;
        if (value < 0x0FFFFFFF) return 4;
        return 8;
    }

    /// <summary>写入 EBML VInt 编码到缓冲区，返回写入字节数</summary>
    private static Int32 WriteVInt(Byte[] buf, Int32 offset, UInt64 value)
    {
        var size = GetVIntSize(value);
        var mask = (Byte)(0xFF >> size);
        buf[offset] = (Byte)(mask | ((size > 1) ? (Byte)(value >> ((size - 1) * 8)) : (Byte)value));
        for (var i = 1; i < size; i++)
            buf[offset + i] = (Byte)(value >> ((size - 1 - i) * 8));
        return size;
    }

    #endregion

    #endregion
}
