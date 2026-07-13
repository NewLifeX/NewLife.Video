using NewLife.Log;
using NewLife.Video.Devices;
using NewLife.Video.Processing;

namespace NewLife.Video.Studio.Services;

/// <summary>设备服务，封装 VideoDeviceManager + 帧渲染</summary>
public class DeviceService : IDeviceService, IDisposable
{
    private readonly VideoDeviceManager _manager = new();
    private IVideoCaptureDevice? _device;
    private Boolean _disposed;

    /// <summary>帧捕获事件</summary>
    public event EventHandler<(Byte[] Data, Int32 Width, Int32 Height, Int32 Stride)>? FrameCaptured;

    /// <summary>当前是否正在捕获</summary>
    public Boolean IsCapturing => _device?.IsCapturing ?? false;

    /// <summary>当前设备名称</summary>
    public String? CurrentDeviceName { get; private set; }

    /// <summary>枚举所有视频捕获设备</summary>
    public IList<VideoDeviceInfo> EnumerateDevices()
    {
        try
        {
            return _manager.GetDevices();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return [];
        }
    }

    /// <summary>打开设备</summary>
    public void OpenDevice(String deviceId, String deviceName)
    {
        StopCapture();

        try
        {
            _device = _manager.OpenDevice(deviceId, 640, 480, 30, VideoTypes.YUV420P);
            CurrentDeviceName = deviceName;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            _device = null;
            CurrentDeviceName = null;
        }
    }

    /// <summary>开始捕获</summary>
    public void StartCapture(Int32 width = 640, Int32 height = 480, Double frameRate = 30, VideoTypes pixelFormat = VideoTypes.YUV420P)
    {
        if (_device == null) return;

        _device.FrameCaptured += OnFrameCaptured;
        _device.StartCapture(width, height, frameRate, pixelFormat);
    }

    /// <summary>停止捕获</summary>
    public void StopCapture()
    {
        if (_device != null)
        {
            _device.FrameCaptured -= OnFrameCaptured;
            _device.StopCapture();
            _device.Dispose();
            _device = null;
        }
        CurrentDeviceName = null;
    }

    private void OnFrameCaptured(Object? sender, VideoFrame frame)
    {
        try
        {
            var bgra = VideoFrameRenderer.ToBgra32(frame);
            FrameCaptured?.Invoke(this, (bgra, frame.Width, frame.Height, frame.Width * 4));
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCapture();
    }
}
