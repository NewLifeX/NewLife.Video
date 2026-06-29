namespace NewLife.Video.Devices;

/// <summary>Windows DirectShow 视频后端骨架。通过 DirectShow 接口枚举和采集摄像头</summary>
/// <remarks>
/// DirectShow 是 Windows 平台的标准视频采集框架。
/// 完整实现需要 COM Interop 或第三方库（如 DirectShowLib）。
/// 当前为骨架实现，提供接口定义和基本结构。
/// </remarks>
public class DirectShowBackend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "DirectShow";

    /// <summary>枚举视频捕获设备</summary>
    /// <returns>设备列表（骨架返回空）</returns>
    public VideoDeviceInfo[] EnumerateDevices()
    {
        // DirectShow 设备枚举需要 COM 接口
        // SystemDeviceEnum → ICreateDevEnum → IEnumMoniker → IMoniker → IPropertyBag
        // 当前为骨架，返回空列表
        return [];
    }

    /// <summary>创建视频捕获设备</summary>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        // DirectShow Capture Graph: FilterGraph → CaptureFilter → SampleGrabber → NullRenderer
        // 当前为骨架
        throw new NotSupportedException("DirectShow 后端尚未实现完整设备采集");
    }
}
