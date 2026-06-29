using NewLife.Video;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest.Devices;

public class V4L2BackendTests
{
    [Fact(DisplayName = "V4L2Backend Name 返回 V4L2")]
    public void Name_ReturnsV4L2()
    {
        var backend = new V4L2Backend();
        Assert.Equal("V4L2", backend.Name);
    }

    [Fact(DisplayName = "V4L2Backend EnumerateDevices 返回数组")]
    public void EnumerateDevices_ReturnsArray()
    {
        var backend = new V4L2Backend();
        var devices = backend.EnumerateDevices();

        Assert.NotNull(devices);
        // Windows 上返回空数组，Linux 上可能返回设备列表
    }

    [Fact(DisplayName = "V4L2Backend CreateCaptureDevice 抛 NotSupportedException")]
    public void CreateCaptureDevice_ThrowsNotSupported()
    {
        var backend = new V4L2Backend();

        var ex = Assert.Throws<NotSupportedException>(() =>
            backend.CreateCaptureDevice("/dev/video0", 640, 480, 30, VideoTypes.YUV420P));

        Assert.Contains("V4L2", ex.Message);
    }

    [Fact(DisplayName = "V4L2Backend 实现 IVideoBackend 接口")]
    public void Implements_IVideoBackend()
    {
        var backend = new V4L2Backend();
        Assert.IsAssignableFrom<IVideoBackend>(backend);
    }
}
