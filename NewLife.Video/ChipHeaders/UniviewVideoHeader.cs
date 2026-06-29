using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>宇视芯片视频头。4字节头: 0x55 0x4E 0x56 0x00 ("UNV")</summary>
public class UniviewVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [(Byte)'U', (Byte)'N', (Byte)'V', 0x00];

    /// <summary>优先级（最低）</summary>
    public Int32 Priority => 3;

    /// <summary>尝试去除宇视视频头</summary>
    public Boolean TryTrim(IPacket data, out IPacket result)
    {
        result = null;
        if (data == null) return false;

        var span = data.GetSpan();
        if (span.Length < 4) return false;

        var reader = new SpanReader(span);
        var magic = reader.ReadBytes(4);
        if (!magic.SequenceEqual((ReadOnlySpan<Byte>)Magic)) return false;

        result = data.Slice(4);
        return true;
    }

    /// <summary>添加宇视视频头</summary>
    public Boolean TryAdd(IPacket data, out IPacket result)
    {
        var span = data.GetSpan();
        var combined = new Byte[4 + data.Total];
        var writer = new SpanWriter(combined);
        writer.Write((ReadOnlySpan<Byte>)Magic);
        span.CopyTo(combined.AsSpan(writer.Position));

        result = new ArrayPacket(combined);
        return true;
    }
}
