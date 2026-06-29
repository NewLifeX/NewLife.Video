using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>宇视芯片视频头。4字节头: 0x55 0x4E 0x56 0x00 ("UNV")</summary>
public class UniviewVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [(Byte)'U', (Byte)'N', (Byte)'V', 0x00];

    /// <summary>优先级（最低）</summary>
    public Int32 Priority => 3;

    /// <summary>尝试去除宇视视频头</summary>
    public Boolean TryTrim(Packet data, out Packet result)
    {
        result = null;
        if (data == null || data.Total < 4) return false;

        var header = data.ReadBytes(0, 4);
        for (var i = 0; i < 4; i++)
            if (header[i] != Magic[i]) return false;

        result = data.Slice(4);
        return true;
    }

    /// <summary>添加宇视视频头</summary>
    public Boolean TryAdd(Packet data, out Packet result)
    {
        var combined = new Byte[4 + data.Total];
        Array.Copy(Magic, 0, combined, 0, 4);
        Array.Copy(data.Data, data.Offset, combined, 4, data.Total);
        result = new Packet(combined);
        return true;
    }
}
