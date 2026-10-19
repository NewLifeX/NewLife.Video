namespace NewLife.Video.Devices;

/// <summary>视频设备管理器。枚举和管理视频捕获设备</summary>
/// <remarks>
/// 跨平台抽象层：Windows (DirectShow)、Linux (V4L2)。
/// 当前为接口骨架，具体后端实现后续完善。
/// </remarks>
public class VideoDeviceManager
{
    private readonly List<IVideoBackend> _backends = [];

    /// <summary>获取所有视频捕获设备</summary>
    /// <returns>设备信息列表</returns>
    public VideoDeviceInfo[] GetDevices()
    {
        var devices = new List<VideoDeviceInfo>();
        foreach (var backend in _backends)
        {
            try
            {
                devices.AddRange(backend.EnumerateDevices());
            }
            catch
            {
                // 某后端枚举失败不影响其他后端
            }
        }
        return devices.ToArray();
    }

    /// <summary>打开视频捕获设备</summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="frameRate">帧率</param>
    /// <param name="pixelFormat">像素格式</param>
    /// <returns>视频捕获设备</returns>
    public IVideoCaptureDevice OpenDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        foreach (var backend in _backends)
        {
            try
            {
                var device = backend.CreateCaptureDevice(deviceId, width, height, frameRate, pixelFormat);
                if (device != null) return device;
            }
            catch
            {
                // 尝试下一个后端
            }
        }

        throw new NotSupportedException(L.T("Unable to open device [{0}]; no available backend supports it.", deviceId));
    }

    /// <summary>注册后端</summary>
    /// <param name="backend">后端适配器</param>
    public void RegisterBackend(IVideoBackend backend)
    {
        if (backend == null) throw new ArgumentNullException(nameof(backend));
        _backends.Add(backend);
    }
}

/// <summary>视频后端适配器接口</summary>
public interface IVideoBackend
{
    /// <summary>后端名称</summary>
    String Name { get; }

    /// <summary>枚举设备</summary>
    /// <returns>设备信息列表</returns>
    VideoDeviceInfo[] EnumerateDevices();

    /// <summary>创建视频捕获设备</summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="frameRate">帧率</param>
    /// <param name="pixelFormat">像素格式</param>
    /// <returns>视频捕获设备（不支持返回 null）</returns>
    IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat);
}
