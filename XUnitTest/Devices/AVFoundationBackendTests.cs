using NewLife.Video;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest.Devices;

public class AVFoundationBackendTests
{
    [Fact(DisplayName = "AVFoundationBackend 实现 IVideoBackend 接口")]
    public void Implements_IVideoBackend()
    {
        var backend = new AVFoundationBackend();

        Assert.IsAssignableFrom<IVideoBackend>(backend);
        Assert.Equal("AVFoundation", backend.Name);
    }

    [Fact(DisplayName = "AVFoundationBackend 枚举设备返回数组")]
    public void EnumerateDevices_ReturnsArray()
    {
        var backend = new AVFoundationBackend();
        var devices = backend.EnumerateDevices();

        Assert.NotNull(devices);
        // 非 macOS 平台返回空数组
        if (!OperatingSystem.IsMacOS())
            Assert.Empty(devices);
    }

    [Fact(DisplayName = "AVFoundationBackend 创建设备抛NotSupported")]
    public void CreateCaptureDevice_ThrowsNotSupported()
    {
        var backend = new AVFoundationBackend();

        Assert.Throws<NotSupportedException>(() =>
            backend.CreateCaptureDevice("test", 640, 480, 30, VideoTypes.RGB24));
    }

    [Fact(DisplayName = "AVFoundationBackend 可在VideoDeviceManager中注册")]
    public void RegisterInVideoDeviceManager()
    {
        var manager = new VideoDeviceManager();
        manager.RegisterBackend(new AVFoundationBackend());

        var devices = manager.GetDevices();
        Assert.NotNull(devices);
    }
}
