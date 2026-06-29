using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>FLV 容器写入器骨架。将视频编码数据封装为 FLV 文件</summary>
public class FlvWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Boolean _headerWritten;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private UInt32 _prevTagSize;

    public FlvWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate;
        WriteFlvHeader();
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;

        if (!_headerWritten) WriteFlvHeader();

        var data = packet.Data.ToArray();

        // FLV Tag Header: 11 bytes (大端)
        // [0] tagType(1) [1-3] dataSize(BE24) [4-6] timestamp(BE24) [7] tsExt(1) [8-10] streamId(3)
        var tagBuf = new Byte[11];
        tagBuf[0] = 9; // video tag
        tagBuf[1] = (Byte)(data.Length >> 16);
        tagBuf[2] = (Byte)(data.Length >> 8);
        tagBuf[3] = (Byte)data.Length;
        var ts = (UInt32)(packet.PTS > 0 ? packet.PTS : 0);
        tagBuf[4] = (Byte)((ts >> 16) & 0xFF);
        tagBuf[5] = (Byte)((ts >> 8) & 0xFF);
        tagBuf[6] = (Byte)(ts & 0xFF);
        tagBuf[7] = (Byte)((ts >> 24) & 0xFF);
        var codecId = _codecType == VideoTypes.H265 ? (Byte)12 : (Byte)7;
        tagBuf[8] = (Byte)((packet.IsKeyFrame ? 1 : 2) << 4 | codecId);
        tagBuf[9] = 0;
        tagBuf[10] = 0;

        _stream.Write(tagBuf, 0, 11);
        _stream.Write(data, 0, data.Length);

        // PreviousTagSize (4 bytes, big-endian)
        _prevTagSize = (UInt32)(11 + data.Length);
        var ptsBuf = new Byte[4];
        var pw = new SpanWriter(ptsBuf) { IsLittleEndian = false };
        pw.Write(_prevTagSize);
        _stream.Write(ptsBuf, 0, 4);
    }

    public void Flush() => _stream.Flush();

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
        _stream?.Dispose();
    }
    private Boolean _disposed;
}
