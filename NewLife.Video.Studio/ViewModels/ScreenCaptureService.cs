using NewLife.Log;
using NewLife.Video.Devices;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>屏幕捕获服务实现，基于 ScreenCaptureBackend</summary>
public class ScreenCaptureService : IScreenCaptureService
{
    private IVideoCaptureDevice? _captureDevice;

    /// <summary>帧捕获事件</summary>
    public event EventHandler<NewLife.Video.VideoFrame>? FrameCaptured;

    /// <summary>是否正在捕获</summary>
    public Boolean IsCapturing => _captureDevice?.IsCapturing ?? false;

    /// <summary>开始捕获</summary>
    public void Start()
    {
        Stop();

        try
        {
            var backend = new ScreenCaptureBackend();
            var devices = backend.EnumerateDevices();
            if (devices == null || devices.Length == 0)
            {
                XTrace.WriteLine("未找到屏幕捕获设备");
                return;
            }

            _captureDevice = backend.CreateCaptureDevice(devices[0].DeviceId, 0, 0, 15, VideoTypes.BGRA32);
            _captureDevice.FrameCaptured += OnFrameCaptured;
            _captureDevice.StartCapture();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>停止捕获</summary>
    public void Stop()
    {
        if (_captureDevice != null)
        {
            _captureDevice.FrameCaptured -= OnFrameCaptured;
            _captureDevice.StopCapture();
            _captureDevice.Dispose();
            _captureDevice = null;
        }
    }

    private void OnFrameCaptured(Object? sender, NewLife.Video.VideoFrame frame)
    {
        FrameCaptured?.Invoke(this, frame);
    }

    public void Dispose()
    {
        Stop();
    }
}
