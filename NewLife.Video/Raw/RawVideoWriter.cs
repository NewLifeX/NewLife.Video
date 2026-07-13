using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.Raw;

/// <summary>原始视频帧写入器。将原始像素数据（YUV/RGB）逐帧写入文件</summary>
public class RawVideoWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _pixelFormat;

    public RawVideoWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _pixelFormat = codecType;
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (packet.Data.TryGetArray(out var seg))
            _stream.Write(seg.Array, seg.Offset, seg.Count);
        else
        {
            var data = packet.Data.ToArray();
            _stream.Write(data, 0, data.Length);
        }
    }

    public void Flush() => _stream.Flush();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
        _stream?.Dispose();
    }
    private Boolean _disposed;
}
