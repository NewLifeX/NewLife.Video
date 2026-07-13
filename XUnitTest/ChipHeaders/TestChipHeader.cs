using System;
using NewLife.Data;
using NewLife.Video;
using NewLife.Video.ChipHeaders;

namespace XUnitTest.ChipHeaders;

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

    public Boolean TryTrim(IPacket data, out IPacket result)
    {
        if (data == null)
        {
            result = null;
            return false;
        }

        var span = data.GetSpan();
        if (span.Length < _magic.Length)
        {
            result = null;
            return false;
        }

        var header = span.Slice(0, _magic.Length);
        var headerStr = System.Text.Encoding.ASCII.GetString(header);
        if (headerStr == _magic)
        {
            result = data.Slice(_magic.Length);
            return true;
        }

        result = null;
        return false;
    }

    public Boolean TryAdd(IPacket data, out IPacket result)
    {
        var span = data.GetSpan();
        var headerBytes = System.Text.Encoding.ASCII.GetBytes(_magic);
        var combined = new Byte[headerBytes.Length + data.Total];
        Array.Copy(headerBytes, 0, combined, 0, headerBytes.Length);
        span.CopyTo(combined.AsSpan(headerBytes.Length));
        result = new ArrayPacket(combined);
        return true;
    }
}
