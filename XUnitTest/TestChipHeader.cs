using System;
using NewLife.Data;
using NewLife.Video;

namespace XUnitTest;

/// <summary>测试用芯片头处理器</summary>
public class TestChipHeader : IVideoChipHeader
{
    private readonly String _magic;
    private readonly Int32 _priority;

    public TestChipHeader(String magic, Int32 priority)
    {
        _magic = magic;
        _priority = priority;
    }

    public Int32 Priority => _priority;

    public Boolean TryTrim(Packet data, out Packet result)
    {
        if (data == null || data.Total < _magic.Length)
        {
            result = null;
            return false;
        }

        var header = data.ReadBytes(0, _magic.Length);
        var headerStr = System.Text.Encoding.ASCII.GetString(header);
        if (headerStr == _magic)
        {
            result = data.Slice(_magic.Length);
            return true;
        }

        result = null;
        return false;
    }

    public Boolean TryAdd(Packet data, out Packet result)
    {
        var headerBytes = System.Text.Encoding.ASCII.GetBytes(_magic);
        var combined = new Byte[headerBytes.Length + data.Total];
        Array.Copy(headerBytes, 0, combined, 0, headerBytes.Length);
        Array.Copy(data.Data, data.Offset, combined, headerBytes.Length, data.Total);
        result = new Packet(combined);
        return true;
    }
}
