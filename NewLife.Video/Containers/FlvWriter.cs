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

        var data = packet.Data.ReadBytes();

        // FLV Tag: 11 bytes header + data
        var tagHeader = new Byte[11];
        tagHeader[0] = 9; // video tag
        // data size (3 bytes, big-endian)
        tagHeader[1] = (Byte)(data.Length >> 16);
        tagHeader[2] = (Byte)(data.Length >> 8);
        tagHeader[3] = (Byte)(data.Length);
        // timestamp (3 bytes, big-endian)
        var ts = (UInt32)(packet.PTS > 0 ? packet.PTS : 0);
        tagHeader[4] = (Byte)((ts >> 16) & 0xFF);
        tagHeader[5] = (Byte)((ts >> 8) & 0xFF);
        tagHeader[6] = (Byte)(ts & 0xFF);
        // timestamp extended
        tagHeader[7] = (Byte)((ts >> 24) & 0xFF);
        // stream ID (3 bytes, always 0)
        // frame type + codec id
        var codecId = _codecType == VideoTypes.H265 ? (Byte)12 : (Byte)7; // 7=AVC, 12=HEVC
        tagHeader[8] = (Byte)((packet.IsKeyFrame ? 1 : 2) << 4 | codecId);

        _stream.Write(tagHeader, 0, 11);
        _stream.Write(data, 0, data.Length);

        // PreviousTagSize (4 bytes, big-endian)
        _prevTagSize = (UInt32)(11 + data.Length);
        _stream.WriteByte((Byte)(_prevTagSize >> 24));
        _stream.WriteByte((Byte)(_prevTagSize >> 16));
        _stream.WriteByte((Byte)(_prevTagSize >> 8));
        _stream.WriteByte((Byte)(_prevTagSize));
    }

    public void Flush() => _stream.Flush();

    private void WriteFlvHeader()
    {
        // FLV Header: "FLV" + version(1) + flags(1) + headerSize(4)
        _stream.WriteByte((Byte)'F');
        _stream.WriteByte((Byte)'L');
        _stream.WriteByte((Byte)'V');
        _stream.WriteByte(1);     // version 1
        _stream.WriteByte(0x01);  // flags: video only
        _stream.WriteByte(0x00);  // header size = 9
        _stream.WriteByte(0x00);
        _stream.WriteByte(0x00);
        _stream.WriteByte(0x09);
        // First PreviousTagSize = 0
        _stream.WriteByte(0x00);
        _stream.WriteByte(0x00);
        _stream.WriteByte(0x00);
        _stream.WriteByte(0x00);

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
