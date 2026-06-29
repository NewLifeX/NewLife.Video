using NewLife.Video;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest.Devices;

public class VideoDeviceManagerTests
{
    [Fact(DisplayName = "VideoDeviceManager初始状态无设备")]
    public void InitialState_NoDevices()
    {
        var manager = new VideoDeviceManager();
        var devices = manager.GetDevices();

        Assert.Empty(devices);
    }

    [Fact(DisplayName = "VideoDeviceManager注册后端后可枚举设备")]
    public void RegisterBackend_EnumeratesDevices()
    {
        var manager = new VideoDeviceManager();
        var backend = new TestVideoBackend();
        manager.RegisterBackend(backend);

        var devices = manager.GetDevices();

        Assert.NotEmpty(devices);
        Assert.Equal("Test Camera", devices[0].Name);
        Assert.Equal("test-001", devices[0].DeviceId);
        Assert.Equal("TestBackend", devices[0].BackendType);
    }

    [Fact(DisplayName = "VideoDeviceManager打开设备成功")]
    public void OpenDevice_ValidInfo_ReturnsDevice()
    {
        var manager = new VideoDeviceManager();
        manager.RegisterBackend(new TestVideoBackend());

        var device = manager.OpenDevice("test-001", 640, 480, 30, VideoTypes.RGB24);

        Assert.NotNull(device);
        Assert.Equal("Test Camera", device.Name);
        Assert.False(device.IsCapturing);
    }

    [Fact(DisplayName = "VideoDeviceManager打开不存在的设备抛异常")]
    public void OpenDevice_InvalidId_Throws()
    {
        var manager = new VideoDeviceManager();

        Assert.Throws<NotSupportedException>(() =>
            manager.OpenDevice("nonexistent", 640, 480, 30, VideoTypes.RGB24));
    }

    [Fact(DisplayName = "VideoDeviceManager注册null后端抛异常")]
    public void RegisterBackend_Null_Throws()
    {
        var manager = new VideoDeviceManager();
        Assert.Throws<ArgumentNullException>(() => manager.RegisterBackend(null));
    }

    [Fact(DisplayName = "VideoCaptureFormat字符串表示正确")]
    public void VideoCaptureFormat_ToString_Correct()
    {
        var format = new VideoCaptureFormat
        {
            Width = 1920,
            Height = 1080,
            MaxFrameRate = 30,
            PixelFormat = VideoTypes.YUV420P,
        };

        var str = format.ToString();
        Assert.Contains("1920", str);
        Assert.Contains("1080", str);
        Assert.Contains("30", str);
    }

    /// <summary>测试用视频后端</summary>
    private class TestVideoBackend : IVideoBackend
    {
        public String Name => "TestBackend";

        public VideoDeviceInfo[] EnumerateDevices()
        {
            return
            [
                new VideoDeviceInfo
                {
                    Name = "Test Camera",
                    DeviceId = "test-001",
                    BackendType = "TestBackend",
                    SupportedFormats =
                    [
                        new VideoCaptureFormat { Width = 640, Height = 480, MaxFrameRate = 30, PixelFormat = VideoTypes.RGB24 },
                        new VideoCaptureFormat { Width = 1280, Height = 720, MaxFrameRate = 15, PixelFormat = VideoTypes.YUV420P },
                    ],
                },
            ];
        }

        public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
        {
            if (deviceId != "test-001") return null;

            return new TestCaptureDevice(width, height, frameRate, pixelFormat);
        }
    }

    /// <summary>测试用视频捕获设备</summary>
    private class TestCaptureDevice : IVideoCaptureDevice
    {
        public String Name => "Test Camera";
        public String DeviceId => "test-001";
        public Int32 VideoWidth { get; }
        public Int32 VideoHeight { get; }
        public Double FrameRate { get; }
        public VideoTypes PixelFormat { get; }
        public IReadOnlyList<VideoCaptureFormat> SupportedFormats => Array.Empty<VideoCaptureFormat>();
        public Boolean IsCapturing { get; private set; }

        public event EventHandler<VideoFrame> FrameCaptured;

        public TestCaptureDevice(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
        {
            VideoWidth = width;
            VideoHeight = height;
            FrameRate = frameRate;
            PixelFormat = pixelFormat;
        }

        public void StartCapture() => IsCapturing = true;
        public void StartCapture(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat) => IsCapturing = true;
        public void StopCapture() => IsCapturing = false;
        public void Dispose() { }
    }
}
