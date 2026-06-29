using NewLife.Video;
using NewLife.Video.Streaming;
using NewLife.Video.IoT;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest;

public class StreamingIoTests
{
    [Fact(DisplayName = "RtspClient URL解析正确")]
    public void RtspClient_ParseUrl_Correct()
    {
        var client = new RtspClient("rtsp://192.168.1.64:554/stream");
        Assert.Equal("rtsp://192.168.1.64:554/stream", client.Url);
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "RtspClient 默认端口")]
    public void RtspClient_DefaultPort()
    {
        var client = new RtspClient("rtsp://camera.local/stream");
        Assert.Contains("camera.local", client.Url);
    }

    [Fact(DisplayName = "RtspClient Dispose安全")]
    public void RtspClient_Dispose_Safe()
    {
        var client = new RtspClient("rtsp://localhost/live");
        client.Dispose();
    }

    [Fact(DisplayName = "Gb28181Registrar 构造正确")]
    public void Gb28181Registrar_Constructor_Correct()
    {
        var reg = new Gb28181Registrar("192.168.1.1", 5060, "34020000001110000001", "3402000000");
        Assert.Equal("34020000001110000001", reg.DeviceId);
        Assert.False(reg.IsRegistered);
    }

    [Fact(DisplayName = "Gb28181Player 构造正确")]
    public void Gb28181Player_Constructor_Correct()
    {
        var player = new Gb28181Player("192.168.1.1", 5060, "34020000001110000001", "3402000000");
        Assert.Equal("34020000001110000001", player.DeviceId);
        Assert.False(player.IsPlaying);
    }

    [Fact(DisplayName = "DirectShowBackend 返回空设备")]
    public void DirectShowBackend_ReturnsEmpty()
    {
        var backend = new DirectShowBackend();
        Assert.Equal("DirectShow", backend.Name);
        Assert.Empty(backend.EnumerateDevices());
    }

    [Fact(DisplayName = "DirectShowBackend 采集不支持")]
    public void DirectShowBackend_Capture_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            new DirectShowBackend().CreateCaptureDevice("any", 640, 480, 30, VideoTypes.RGB24));
    }
}
