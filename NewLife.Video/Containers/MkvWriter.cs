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
        var data = packet.Data.ReadBytes();
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

        // Info
        WriteEbmlId(_stream, 0x1549A966);
        _infoSizePos = _stream.Position;
        WriteEbmlSize(_stream, 0); // placeholder
        var infoStart = _stream.Position;
        WriteEbmlUint(_stream, 0x2AD7B1, 1000000);  // TimecodeScale (1ms)
        WriteEbmlFloat(_stream, 0x4489, 25.0);        // Duration (placeholder)
        WriteEbmlString(_stream, 0x4D80, "NewLife.Video");
        WriteEbmlString(_stream, 0x5741, "NewLife.Video MkvWriter");
        var infoSize = (UInt64)(_stream.Position - infoStart);
        var cur = _stream.Position;
        _stream.Seek(_infoSizePos, SeekOrigin.Begin);
        WriteEbmlSize(_stream, infoSize);
        _stream.Seek(cur, SeekOrigin.Begin);

        // Tracks
        WriteEbmlId(_stream, 0x1654AE6B);
        WriteEbmlSize(_stream, 40);
        WriteEbmlId(_stream, 0xAE); // TrackEntry
        WriteEbmlSize(_stream, 30);
        WriteEbmlUint(_stream, 0xD7, 1);       // TrackNumber
        WriteEbmlUint(_stream, 0x73C5, 1);     // TrackUID
        WriteEbmlUint(_stream, 0x83, 1);       // TrackType = video
        WriteEbmlUint(_stream, 0x9C, 0);       // FlagLacing
        WriteEbmlString(_stream, 0x86, _codecType == VideoTypes.VP9 ? "V_VP9" : _codecType == VideoTypes.VP8 ? "V_VP8" : "V_MPEG4/ISO/AVC");
        WriteEbmlUint(_stream, 0xE0, (UInt64)_width);  // PixelWidth
        WriteEbmlUint(_stream, 0xBA, (UInt64)_height); // PixelHeight

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

    private Int64 _infoSizePos;

    private static void WriteEbmlId(FileStream fs, UInt32 id)
    {
        if (id >= 0x10000000) { fs.WriteByte((Byte)(id >> 24)); fs.WriteByte((Byte)(id >> 16)); fs.WriteByte((Byte)(id >> 8)); fs.WriteByte((Byte)id); }
        else if (id >= 0x100000) { fs.WriteByte((Byte)(id >> 16)); fs.WriteByte((Byte)(id >> 8)); fs.WriteByte((Byte)id); }
        else if (id >= 0x1000) { fs.WriteByte((Byte)(id >> 8)); fs.WriteByte((Byte)id); }
        else fs.WriteByte((Byte)id);
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
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        fs.Write(bytes, 0, 8);
    }

    private static void WriteUInt16BE(FileStream fs, UInt16 v)
    {
        fs.WriteByte((Byte)(v >> 8));
        fs.WriteByte((Byte)v);
    }

    private static void WriteBytes(FileStream fs, Byte[] data) => fs.Write(data, 0, data.Length);

    #endregion
}
