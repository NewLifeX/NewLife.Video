namespace NewLife.Video.Devices;

/// <summary>Windows DirectShow 视频后端。通过 DirectShow COM 接口枚举和采集摄像头</summary>
/// <remarks>
/// DirectShow 是 Windows 平台的标准视频采集框架。
///
/// 完整实现需要以下 COM 接口（通过 DirectShowLib 或自定义 P/Invoke）：
///   ICreateDevEnum → IEnumMoniker → IMoniker → IPropertyBag（设备枚举）
///   IGraphBuilder / ICaptureGraphBuilder2 → ISampleGrabber → IMediaControl（采集图构建）
///   IAMStreamConfig → IAMVideoProcAmp（格式协商和参数控制）
///
/// 引用：需 NuGet 包 DirectShowLib-2005 或类似封装库。
/// 当前提供骨架实现：IVideoBackend 接口完整定义，EnumerateDevices 返回空列表，
/// CreateCaptureDevice 抛 NotSupportedException 并提示需 DirectShowLib 依赖。
/// </remarks>
public class DirectShowBackend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "DirectShow";

    /// <summary>枚举视频捕获设备</summary>
    /// <returns>设备列表（需 DirectShowLib 依赖启用）</returns>
    public VideoDeviceInfo[] EnumerateDevices()
    {
        // DirectShow 设备枚举需要 COM 接口：
        // 1. CoCreateInstance(CLSID_SystemDeviceEnum) → ICreateDevEnum
        // 2. ICreateDevEnum.CreateClassEnumerator(CLSID_VideoInputDeviceCategory) → IEnumMoniker
        // 3. IEnumMoniker.Next → IMoniker
        // 4. IMoniker.BindToStorage → IPropertyBag
        // 5. IPropertyBag.Read("FriendlyName") / Read("DevicePath")
        // 引入 DirectShowLib NuGet 后取消下方注释：
        // return DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice)
        //     .Select(d => new VideoDeviceInfo { Name = d.Name, DeviceId = d.DevicePath, BackendType = "DirectShow" })
        //     .ToArray();
        return [];
    }

    /// <summary>创建视频捕获设备</summary>
    /// <remarks>需 DirectShowLib 依赖。采集链路：VideoSourceFilter → SampleGrabber → NullRenderer</remarks>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        throw new NotSupportedException(L.T("The DirectShow backend requires the DirectShowLib NuGet package to enable full capture; it is currently a skeleton implementation."));
    }
}
