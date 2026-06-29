using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>大华芯片视频头。6字节头: 0xDA 0xAH 0xUA 0x00 + 2字节类型</summary>
public class DahuaVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [0xDA, 0x41, 0x48, 0x00]; // vendor magic

    /// <summary>优先级（低于海思）</summary>
    public Int32 Priority => 2;

    /// <summary>尝试去除大华视频头</summary>
    public Boolean TryTrim(Packet data, out Packet result)
    {
        result = null;
        if (data == null || data.Total < 6) return false;

        var header = data.ReadBytes(0, 4);
        for (var i = 0; i < 4; i++)
            if (header[i] != Magic[i]) return false;

        result = data.Slice(6);
        return true;
    }

    /// <summary>添加大华视频头</summary>
    public Boolean TryAdd(Packet data, out Packet result)
    {
        var header = new Byte[6];
        Array.Copy(Magic, 0, header, 0, 4);
        header[4] = 0x00; // frame type
        header[5] = 0x01; // sub type

        var combined = new Byte[6 + data.Total];
        Array.Copy(header, 0, combined, 0, 6);
        Array.Copy(data.Data, data.Offset, combined, 6, data.Total);
        result = new Packet(combined);
        return true;
    }
}
