using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

/// <summary>AMF0 解码器测试，覆盖 RTMP 命令响应解析</summary>
public class Amf0DecoderTests
{
    [Fact(DisplayName = "Amf0Decoder 基本类型往返一致")]
    public void Primitives_RoundTrip()
    {
        using var enc = new Amf0Encoder();
        enc.WriteNumber(42.5);
        enc.WriteBoolean(true);
        enc.WriteBoolean(false);
        enc.WriteString("hello");
        enc.WriteNull();

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.TryReadNumber(out var number));
        Assert.Equal(42.5, number, 6);
        Assert.True(dec.TryReadBoolean(out var trueValue));
        Assert.True(trueValue);
        Assert.True(dec.TryReadBoolean(out var falseValue));
        Assert.False(falseValue);
        Assert.True(dec.TryReadString(out var text));
        Assert.Equal("hello", text);
        Assert.True(dec.TryReadNull());
        Assert.True(dec.IsEnd);
    }

    [Fact(DisplayName = "Amf0Decoder 读取 Object 属性字典")]
    public void TryReadObject_WithProperties_Works()
    {
        using var enc = new Amf0Encoder();
        enc.WriteObjectBegin();
        enc.WriteObjectProperty("app", "live");
        enc.WriteObjectProperty("fpad", false);
        enc.WriteObjectProperty("tcUrl", "rtmp://localhost/live");
        enc.WriteObjectEnd();

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.TryReadObject(out var props));
        Assert.Equal(3, props.Count);
        Assert.Equal("live", props["app"]);
        Assert.Equal(false, props["fpad"]);
        Assert.Equal("rtmp://localhost/live", props["tcUrl"]);
    }

    [Fact(DisplayName = "Amf0Decoder 解析 createStream 响应")]
    public void TryReadCreateStreamResult_Works()
    {
        using var enc = new Amf0Encoder();
        enc.WriteString("_result");
        enc.WriteNumber(2);
        enc.WriteNull();
        enc.WriteNumber(1);

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.TryReadCreateStreamResult(out var streamId));
        Assert.Equal(1, streamId);
    }

    [Fact(DisplayName = "Amf0Decoder 解析 connect 成功响应")]
    public void TryReadConnectResult_Success()
    {
        using var enc = new Amf0Encoder();
        enc.WriteString("_result");
        enc.WriteNumber(1);
        enc.WriteObjectBegin();
        enc.WriteObjectProperty("level", "status");
        enc.WriteObjectProperty("code", "NetConnection.Connect.Success");
        enc.WriteObjectEnd();

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.TryReadConnectResult(out var success, out var info));
        Assert.True(success);
        Assert.Equal("NetConnection.Connect.Success", info["code"]);
    }

    [Fact(DisplayName = "Amf0Decoder 解析 connect 失败响应")]
    public void TryReadConnectResult_Error()
    {
        using var enc = new Amf0Encoder();
        enc.WriteString("_error");
        enc.WriteNumber(1);
        enc.WriteNull();

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.TryReadConnectResult(out var success, out var info));
        Assert.False(success);
        Assert.Empty(info);
    }

    [Fact(DisplayName = "Amf0Decoder 截断数据返回 false 不抛异常")]
    public void TruncatedData_ReturnsFalse()
    {
        // 声明长度为 5，实际只有 1 个字节
        var dec = new Amf0Decoder([0x02, 0x00, 0x05, (Byte)'h']);

        Assert.False(dec.TryReadString(out _));
        Assert.False(new Amf0Decoder([]).TryReadNumber(out _));
        Assert.False(new Amf0Decoder([0x01]).TryReadBoolean(out _));
    }

    [Fact(DisplayName = "Amf0Decoder SkipValue 跳过任意类型")]
    public void SkipValue_SkipsAnyType()
    {
        using var enc = new Amf0Encoder();
        enc.WriteObjectBegin();
        enc.WriteObjectProperty("code", "ok");
        enc.WriteObjectEnd();
        enc.WriteString("next");

        var dec = new Amf0Decoder(enc.ToArray());

        Assert.True(dec.SkipValue());
        Assert.True(dec.TryReadString(out var next));
        Assert.Equal("next", next);
    }
}
