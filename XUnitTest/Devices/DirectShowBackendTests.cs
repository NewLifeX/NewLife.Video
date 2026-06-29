using NewLife.Video;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest.Devices;

public class DirectShowBackendTests
{
    [Fact(DisplayName = "DirectShowBackend 返回空设备")]
    public void ReturnsEmpty()
    {
        var backend = new DirectShowBackend();
        Assert.Equal("DirectShow", backend.Name);
        Assert.Empty(backend.EnumerateDevices());
    }

    [Fact(DisplayName = "DirectShowBackend 采集不支持")]
    public void Capture_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            new DirectShowBackend().CreateCaptureDevice("any", 640, 480, 30, VideoTypes.RGB24));
    }

    [Fact(DisplayName = "DirectShowBackend 实现 IVideoBackend")]
    public void Implements_IVideoBackend()
    {
        var backend = new DirectShowBackend();
        Assert.IsAssignableFrom<IVideoBackend>(backend);
    }
}
