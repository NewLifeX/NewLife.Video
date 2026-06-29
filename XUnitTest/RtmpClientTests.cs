using NewLife.Video;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

public class RtmpClientTests
{
    [Fact(DisplayName = "RtmpClient解析标准URL")]
    public void ParseUrl_Standard_Correct()
    {
        var client = new RtmpClient("rtmp://192.168.1.100/live/stream");

        Assert.Equal("192.168.1.100", client.Host);
        Assert.Equal(1935, client.Port);
        Assert.Equal("live", client.AppName);
        Assert.Equal("stream", client.StreamName);
    }

    [Fact(DisplayName = "RtmpClient解析带端口URL")]
    public void ParseUrl_WithPort_Correct()
    {
        var client = new RtmpClient("rtmp://server.com:19350/app/name");

        Assert.Equal("server.com", client.Host);
        Assert.Equal(19350, client.Port);
        Assert.Equal("app", client.AppName);
        Assert.Equal("name", client.StreamName);
    }

    [Fact(DisplayName = "RtmpClient非rtmp协议抛异常")]
    public void ParseUrl_InvalidScheme_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RtmpClient("http://server.com/app/name"));
    }

    [Fact(DisplayName = "RtmpClient无效格式抛异常")]
    public void ParseUrl_InvalidFormat_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RtmpClient("rtmp://nopath"));
    }

    [Fact(DisplayName = "RtmpClient初始状态未连接")]
    public void InitialState_NotConnected()
    {
        var client = new RtmpClient("rtmp://localhost/live/test");

        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "RtmpClient Dispose安全")]
    public void Dispose_CanDispose()
    {
        var client = new RtmpClient("rtmp://localhost/live/test");
        client.Dispose();
        client.Dispose(); // 不应抛异常
    }
}
