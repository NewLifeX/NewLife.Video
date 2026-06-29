namespace NewLife.Video.Devices;

/// <summary>屏幕捕获后端骨架。截取屏幕画面作为视频帧</summary>
/// <remarks>
/// Windows: DXGI Desktop Duplication API (需要 GPU 驱动支持)
/// Linux: X11 SHM / Wayland PipeWire
/// macOS: AVFoundation / CGDisplay
/// 当前为骨架实现。
/// </remarks>
public class ScreenCaptureBackend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "ScreenCapture";

    /// <summary>枚举屏幕捕获源（虚拟设备）</summary>
    public VideoDeviceInfo[] EnumerateDevices()
    {
        return
        [
            new VideoDeviceInfo
            {
                Name = "Screen Capture",
                DeviceId = "screen:0",
                BackendType = "ScreenCapture",
                SupportedFormats =
                [
                    new VideoCaptureFormat { Width = 1920, Height = 1080, MaxFrameRate = 30, PixelFormat = VideoTypes.RGB24 },
                ],
            },
        ];
    }

    /// <summary>创建屏幕捕获设备</summary>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        if (deviceId == "screen:0")
        {
            return new ScreenCaptureDevice(width, height, frameRate, pixelFormat);
        }
        return null;
    }
}

/// <summary>屏幕捕获设备骨架</summary>
internal class ScreenCaptureDevice : IVideoCaptureDevice
{
    private Boolean _capturing;
    private System.Threading.Timer _timer;

    public String Name => "Screen Capture";
    public String DeviceId => "screen:0";
    public Int32 VideoWidth { get; }
    public Int32 VideoHeight { get; }
    public Double FrameRate { get; }
    public VideoTypes PixelFormat { get; }
    public IReadOnlyList<VideoCaptureFormat> SupportedFormats => Array.Empty<VideoCaptureFormat>();
    public Boolean IsCapturing => _capturing;

    public event EventHandler<VideoFrame> FrameCaptured;

    public ScreenCaptureDevice(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        VideoWidth = width;
        VideoHeight = height;
        FrameRate = frameRate;
        PixelFormat = pixelFormat;
    }

    public void StartCapture() => StartCapture(VideoWidth, VideoHeight, FrameRate, PixelFormat);

    public void StartCapture(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        _capturing = true;
        var interval = (Int32)(1000.0 / frameRate);
        _timer = new System.Threading.Timer(_ =>
        {
            if (!_capturing) return;
            var frame = VideoFrame.Create(width, height, VideoTypes.RGB24);
            frame.Timestamp = DateTime.Now.Ticks / 10000;
            FrameCaptured?.Invoke(this, frame);
        }, null, interval, interval);
    }

    public void StopCapture()
    {
        _capturing = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => StopCapture();
}
