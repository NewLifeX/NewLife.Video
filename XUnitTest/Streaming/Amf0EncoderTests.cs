using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class Amf0EncoderTests
{
    [Fact(DisplayName = "Amf0Encoder 编码String正确")]
    public void String_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteString("hello");
        var data = enc.ToArray();
        // type marker (0x02) + 2-byte length + "hello"
        Assert.Equal(0x02, data[0]);
        Assert.Equal(0x00, data[1]);
        Assert.Equal(0x05, data[2]); // length = 5
        Assert.Equal((Byte)'h', data[3]);
    }

    [Fact(DisplayName = "Amf0Encoder 编码Number正确")]
    public void Number_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteNumber(42.0);
        var data = enc.ToArray();
        Assert.Equal(0x00, data[0]); // type marker
        Assert.Equal(9, data.Length); // 1 + 8
    }

    [Fact(DisplayName = "Amf0Encoder 编码Null正确")]
    public void Null_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteNull();
        var data = enc.ToArray();
        Assert.Equal(0x05, data[0]);
        Assert.Single(data);
    }

    [Fact(DisplayName = "Amf0Encoder 编码Boolean正确")]
    public void Boolean_True_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteBoolean(true);
        var data = enc.ToArray();
        Assert.Equal(0x01, data[0]);
        Assert.Equal(0x01, data[1]);
    }

    [Fact(DisplayName = "Amf0Encoder 编码Boolean false正确")]
    public void Boolean_False_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteBoolean(false);
        var data = enc.ToArray();
        Assert.Equal(0x01, data[0]);
        Assert.Equal(0x00, data[1]);
    }

    [Fact(DisplayName = "Amf0Encoder ObjectEnd 写入结束标记")]
    public void ObjectEnd_WritesTerminator()
    {
        using var enc = new Amf0Encoder();
        enc.WriteObjectBegin();
        enc.WriteObjectProperty("app", "live");
        enc.WriteObjectEnd();

        var data = enc.ToArray();

        Assert.Equal(new Byte[] { 0x00, 0x00, 0x09 }, data[^3..]);
    }
}
