using NewLife.Video;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest.Devices;

public class ScreenCaptureBackendTests
{
    [Fact(DisplayName = "ScreenCaptureBackend 枚举设备正确")]
    public void EnumerateDevices_ReturnsDevice()
    {
        var backend = new ScreenCaptureBackend();
        var devices = backend.EnumerateDevices();
        Assert.NotEmpty(devices);
        Assert.Equal("screen:0", devices[0].DeviceId);
        Assert.Equal("ScreenCapture", devices[0].BackendType);
    }

    [Fact(DisplayName = "ScreenCaptureBackend 创建设备返回ScreenCaptureDevice")]
    public void CreateDevice_ReturnsNonnull()
    {
        var backend = new ScreenCaptureBackend();
        var device = backend.CreateCaptureDevice("screen:0", 640, 480, 10, VideoTypes.RGB24);
        Assert.NotNull(device);
        Assert.Equal("screen:0", device.DeviceId);
    }

    [Fact(DisplayName = "ScreenCaptureBackend 创建无效id返回null")]
    public void InvalidId_ReturnsNull()
    {
        var backend = new ScreenCaptureBackend();
        var device = backend.CreateCaptureDevice("invalid", 640, 480, 10, VideoTypes.RGB24);
        Assert.Null(device);
    }

    [Fact(DisplayName = "ScreenCaptureBackend 实现 IVideoBackend")]
    public void Implements_IVideoBackend()
    {
        var backend = new ScreenCaptureBackend();
        Assert.IsAssignableFrom<IVideoBackend>(backend);
    }
}
