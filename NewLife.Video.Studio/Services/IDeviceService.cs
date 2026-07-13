using NewLife.Video.Devices;

namespace NewLife.Video.Studio.Services;

/// <summary>设备服务接口</summary>
public interface IDeviceService
{
    /// <summary>枚举所有视频捕获设备</summary>
    IList<VideoDeviceInfo> EnumerateDevices();

    /// <summary>当前是否正在捕获</summary>
    Boolean IsCapturing { get; }

    /// <summary>当前设备名称</summary>
    String? CurrentDeviceName { get; }

    /// <summary>帧捕获事件</summary>
    event EventHandler<(Byte[] Data, Int32 Width, Int32 Height, Int32 Stride)>? FrameCaptured;

    /// <summary>打开设备</summary>
    void OpenDevice(String deviceId, String deviceName);

    /// <summary>开始捕获</summary>
    void StartCapture(Int32 width = 640, Int32 height = 480, Double frameRate = 30, VideoTypes pixelFormat = VideoTypes.YUV420P);

    /// <summary>停止捕获</summary>
    void StopCapture();
}
