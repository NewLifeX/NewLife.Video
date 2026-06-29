using NewLife.Data;

namespace NewLife.Video.ChipHeaders;

/// <summary>海思芯片视频头。4字节头: 0x48 0x49 0x53 0x49 ("HISI") + 4字节长度</summary>
public class HisiliconVideoHeader : IVideoChipHeader
{
    private static readonly Byte[] Magic = [(Byte)'H', (Byte)'I', (Byte)'S', (Byte)'I'];

    /// <summary>优先级</summary>
    public Int32 Priority => 1;

    /// <summary>尝试去除海思视频头</summary>
    public Boolean TryTrim(Packet data, out Packet result)
    {
        result = null;
        if (data == null || data.Total < 8) return false;

        var header = data.ReadBytes(0, 4);
        for (var i = 0; i < 4; i++)
            if (header[i] != Magic[i]) return false;

        // 读取后续 4 字节长度
        var len = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
        if (len != data.Total - 8) return false;

        result = data.Slice(8);
        return true;
    }

    /// <summary>添加海思视频头</summary>
    public Boolean TryAdd(Packet data, out Packet result)
    {
        var header = new Byte[8];
        Array.Copy(Magic, 0, header, 0, 4);
        var len = data.Total;
        header[4] = (Byte)(len >> 24);
        header[5] = (Byte)(len >> 16);
        header[6] = (Byte)(len >> 8);
        header[7] = (Byte)(len);

        var combined = new Byte[8 + data.Total];
        Array.Copy(header, 0, combined, 0, 8);
        Array.Copy(data.Data, data.Offset, combined, 8, data.Total);
        result = new Packet(combined);
        return true;
    }
}
