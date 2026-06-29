using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MP4 容器写入器骨架。将 H.264/H.265 编码数据封装为 MP4 文件</summary>
public class Mp4Writer : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private readonly List<UInt32> _sampleSizes = [];
    private Int32 _width;
    private Int32 _height;

    public Mp4Writer(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        var data = packet.Data.ReadBytes();
        _stream.Write(data, 0, data.Length);
        _sampleSizes.Add((UInt32)data.Length);
    }

    public void Flush() => _stream.Flush();

    public void Dispose()
    {
        Flush();
        _stream?.Dispose();
    }
}

