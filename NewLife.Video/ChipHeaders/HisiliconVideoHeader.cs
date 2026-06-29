using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>海思芯片视频头。4字节头: 0x48 0x49 0x53 0x49 ("HISI") + 4字节长度</summary>
public class HisiliconVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [(Byte)'H', (Byte)'I', (Byte)'S', (Byte)'I'];

    /// <summary>优先级</summary>
    public Int32 Priority => 1;

    /// <summary>尝试去除海思视频头</summary>
    public Boolean TryTrim(IPacket data, out IPacket result)
    {
        result = null;
        if (data == null) return false;

        var span = data.GetSpan();
        if (span.Length < 8) return false;

        var reader = new SpanReader(span) { IsLittleEndian = false };
        var magic = reader.ReadBytes(4);
        if (!magic.SequenceEqual((ReadOnlySpan<Byte>)Magic)) return false;

        var len = reader.ReadInt32();
        if (len != data.Total - 8) return false;

        result = data.Slice(8);
        return true;
    }

    /// <summary>添加海思视频头</summary>
    public Boolean TryAdd(IPacket data, out IPacket result)
    {
        var span = data.GetSpan();
        var combined = new Byte[8 + data.Total];
        var writer = new SpanWriter(combined) { IsLittleEndian = false };
        writer.Write((ReadOnlySpan<Byte>)Magic);
        writer.Write((Int32)data.Total);
        span.CopyTo(combined.AsSpan(writer.Position));

        result = new ArrayPacket(combined);
        return true;
    }
}
