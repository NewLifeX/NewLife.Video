using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>AVI 容器写入器骨架</summary>
public class AviWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private readonly List<Byte[]> _frames = [];
    private Int32 _width;
    private Int32 _height;
    private Int32 _maxFrameSize;

    public AviWriter(String filePath)
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
        _frames.Add(data);
        if (data.Length > _maxFrameSize) _maxFrameSize = data.Length;
    }

    public void Flush()
    {
        WriteRiffHeader();
        WriteHdrlList();
        WriteMoviList();
        _stream.Flush();
    }

    private void WriteRiffHeader()
    {
        _stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        _stream.WriteLE((UInt32)(_stream.Length + 4)); // placeholder
        _stream.Write(System.Text.Encoding.ASCII.GetBytes("AVI "));
    }

    private void WriteHdrlList()
    {
        // 简化的 hdrl，后续完善
    }

    private void WriteMoviList()
    {
        foreach (var frame in _frames)
        {
            _stream.Write(System.Text.Encoding.ASCII.GetBytes("00dc"));
            _stream.WriteLE((UInt32)frame.Length);
            _stream.Write(frame, 0, frame.Length);
        }
    }

    public void Dispose()
    {
        Flush();
        _stream?.Dispose();
    }
}

internal static class LeExtensions
{
    public static void WriteLE(this Stream s, UInt32 v)
    {
        s.WriteByte((Byte)v); s.WriteByte((Byte)(v >> 8));
        s.WriteByte((Byte)(v >> 16)); s.WriteByte((Byte)(v >> 24));
    }
}
