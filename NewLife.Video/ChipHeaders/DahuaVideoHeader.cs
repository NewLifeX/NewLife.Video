using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>大华芯片视频头。6字节头: 0xDA 0xAH 0xUA 0x00 + 2字节类型</summary>
public class DahuaVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [0xDA, 0x41, 0x48, 0x00]; // vendor magic

    /// <summary>优先级（低于海思）</summary>
    public Int32 Priority => 2;

    /// <summary>尝试去除大华视频头</summary>
    public Boolean TryTrim(IPacket data, out IPacket result)
    {
        result = null;
        if (data == null) return false;

        var span = data.GetSpan();
        if (span.Length < 6) return false;

        var reader = new SpanReader(span);
        var magic = reader.ReadBytes(4);
        if (!magic.SequenceEqual((ReadOnlySpan<Byte>)Magic)) return false;

        result = data.Slice(6);
        return true;
    }

    /// <summary>添加大华视频头</summary>
    public Boolean TryAdd(IPacket data, out IPacket result)
    {
        var span = data.GetSpan();
        var combined = new Byte[6 + data.Total];
        var writer = new SpanWriter(combined);
        writer.Write((ReadOnlySpan<Byte>)Magic);
        writer.WriteByte(0x00); // frame type
        writer.WriteByte(0x01); // sub type
        span.CopyTo(combined.AsSpan(writer.Position));

        result = new ArrayPacket(combined);
        return true;
    }
}
