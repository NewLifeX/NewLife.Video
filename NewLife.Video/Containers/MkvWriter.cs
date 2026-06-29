using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.Containers;

/// <summary>MKV (Matroska/EBML) 容器写入器骨架</summary>
/// <remarks>EBML 头 + Segment + Info/Tracks/Cluster 结构。当前为骨架实现。</remarks>
public class MkvWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Boolean _headerWritten;

    public MkvWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        if (!_headerWritten) WriteHeader();
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (!_headerWritten) WriteHeader();
        var data = packet.Data.ToArray();
        // 简化：直接写 SimpleBlock 结构
        WriteEbmlId(_stream, 0xA3); // SimpleBlock
        var blockLen = 1 + 2 + 1 + data.Length; // track + timecode + flags + data
        WriteEbmlSize(_stream, (UInt64)blockLen);
        WriteEbmlSize(_stream, 0x81); // track number = 1
        WriteUInt16BE(_stream, (UInt16)(_frameCount * 40)); // timecode in ms
        _stream.WriteByte(packet.IsKeyFrame ? (Byte)0x80 : (Byte)0x00); // keyframe flag
        _stream.Write(data, 0, data.Length);
        _frameCount++;
    }

    public void Flush() => _stream.Flush();

    private Int32 _frameCount;
    private Boolean _disposed;

    private void WriteHeader()
    {
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

        // Info — 预计算大小，不依赖回填
        WriteEbmlId(_stream, 0x1549A966);
        var infoContent = BuildInfoContent();
        WriteEbmlSize(_stream, (UInt64)infoContent.Length);
        _stream.Write(infoContent, 0, infoContent.Length);

        // Tracks — 预计算内容
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

    #region EBML 工具

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
        using var ms = new System.IO.MemoryStream(256);
        // TrackEntry
        var trackEntryContent = BuildTrackEntryContent();
        WriteEbmlIdToStream(ms, 0xAE);
        WriteEbmlSizeToStream(ms, (UInt64)trackEntryContent.Length);
        ms.Write(trackEntryContent, 0, trackEntryContent.Length);
        return ms.ToArray();
    }

    private Byte[] BuildTrackEntryContent()
    {
        using var ms = new System.IO.MemoryStream(256);
        var codecId = _codecType == VideoTypes.VP9 ? "V_VP9" : _codecType == VideoTypes.VP8 ? "V_VP8" : "V_MPEG4/ISO/AVC";
        WriteEbmlUintToStream(ms, 0xD7, 1);           // TrackNumber
        WriteEbmlUintToStream(ms, 0x73C5, 1);         // TrackUID
        WriteEbmlUintToStream(ms, 0x83, 1);           // TrackType = video
        WriteEbmlUintToStream(ms, 0x9C, 0);           // FlagLacing
        WriteEbmlStringToStream(ms, 0x86, codecId);   // CodecID
        WriteEbmlUintToStream(ms, 0xE0, (UInt64)_width);  // PixelWidth
        WriteEbmlUintToStream(ms, 0xBA, (UInt64)_height); // PixelHeight
        return ms.ToArray();
    }

    private void WriteEbmlUintToStream(System.IO.MemoryStream ms, UInt32 id, UInt64 value)
    {
        WriteEbmlIdToStream(ms, id);
        var sz = value < 0x100 ? 1 : value < 0x10000 ? 2 : value < 0x1000000 ? 3 : 4;
        WriteEbmlSizeToStream(ms, (UInt64)sz);
        for (var i = sz - 1; i >= 0; i--)
            ms.WriteByte((Byte)(value >> (i * 8)));
    }

    private void WriteEbmlFloatToStream(System.IO.MemoryStream ms, UInt32 id, Double value)
    {
        WriteEbmlIdToStream(ms, id);
        WriteEbmlSizeToStream(ms, 8);
        var buf = new Byte[8];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(value);
        ms.Write(buf, 0, 8);
    }

    private void WriteEbmlStringToStream(System.IO.MemoryStream ms, UInt32 id, String value)
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

    private static void WriteEbmlId(FileStream fs, UInt32 id)
    {
        // 统一 4 字节原始 ID（兼容 MkvReader）
        fs.WriteByte((Byte)(id >> 24));
        fs.WriteByte((Byte)(id >> 16));
        fs.WriteByte((Byte)(id >> 8));
        fs.WriteByte((Byte)id);
    }

    private static void WriteEbmlSize(FileStream fs, UInt64 size)
    {
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
        WriteEbmlSize(fs, (UInt64)(value < 0x100 ? 1 : value < 0x10000 ? 2 : value < 0x1000000 ? 3 : 4));
        if (value >= 0x1000000) { fs.WriteByte((Byte)(value >> 24)); fs.WriteByte((Byte)(value >> 16)); fs.WriteByte((Byte)(value >> 8)); fs.WriteByte((Byte)value); }
        else if (value >= 0x10000) { fs.WriteByte((Byte)(value >> 16)); fs.WriteByte((Byte)(value >> 8)); fs.WriteByte((Byte)value); }
        else if (value >= 0x100) { fs.WriteByte((Byte)(value >> 8)); fs.WriteByte((Byte)value); }
        else fs.WriteByte((Byte)value);
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

    private static void WriteUInt16BE(FileStream fs, UInt16 v)
    {
        var buf = new Byte[2];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(v);
        fs.Write(buf, 0, 2);
    }

    private static void WriteBytes(FileStream fs, Byte[] data) => fs.Write(data, 0, data.Length);

    #endregion
}
