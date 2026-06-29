namespace NewLife.Video.Devices;

/// <summary>Linux V4L2 (Video4Linux2) 视频后端骨架。通过 V4L2 接口枚举和采集摄像头</summary>
/// <remarks>
/// V4L2 是 Linux 平台的标准视频采集框架，通过 /dev/video* 设备文件访问。
/// 完整实现需要 ioctl 系统调用（通过 P/Invoke 或 libv4l2）。
/// 当前为骨架实现，提供接口定义。
/// </remarks>
public class V4L2Backend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "V4L2";

    /// <summary>枚举视频捕获设备</summary>
    /// <returns>设备列表（骨架返回空）</returns>
    public VideoDeviceInfo[] EnumerateDevices()
    {
#if NET5_0_OR_GREATER
        // Linux 下检查 /dev/video* 设备
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var devices = new List<VideoDeviceInfo>();
            for (var i = 0; i < 10; i++)
            {
                var path = $"/dev/video{i}";
                if (File.Exists(path))
                {
                    devices.Add(new VideoDeviceInfo
                    {
                        Name = $"Video{i}",
                        DeviceId = path,
                        BackendType = "V4L2",
                    });
                }
            }
            return devices.ToArray();
        }
#endif
        return [];
    }

    /// <summary>创建视频捕获设备</summary>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        throw new NotSupportedException("V4L2 后端尚未实现完整设备采集");
    }
}
