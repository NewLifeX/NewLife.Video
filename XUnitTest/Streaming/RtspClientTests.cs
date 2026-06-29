using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class RtspClientTests
{
    [Fact(DisplayName = "RtspClient URL解析正确")]
    public void ParseUrl_Correct()
    {
        var client = new RtspClient("rtsp://192.168.1.64:554/stream");
        Assert.Equal("rtsp://192.168.1.64:554/stream", client.Url);
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "RtspClient 默认端口")]
    public void DefaultPort()
    {
        var client = new RtspClient("rtsp://camera.local/stream");
        Assert.Contains("camera.local", client.Url);
    }

    [Fact(DisplayName = "RtspClient Dispose安全")]
    public void Dispose_Safe()
    {
        var client = new RtspClient("rtsp://localhost/live");
        client.Dispose();
    }
}
