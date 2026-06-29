using System.Runtime.InteropServices;

namespace NewLife.Video.Devices;

/// <summary>Linux V4L2 (Video4Linux2) 视频后端。通过 /dev/video* 和 ioctl 接口枚举和采集摄像头</summary>
/// <remarks>
/// V4L2 是 Linux 平台的标准视频采集框架。
///
/// 完整实现需要的 ioctl 命令和结构体：
///   VIDIOC_QUERYCAP (v4l2_capability) — 设备能力查询
///   VIDIOC_ENUM_FMT / VIDIOC_ENUM_FRAMESIZES / VIDIOC_ENUM_FRAMEINTERVALS — 格式枚举
///   VIDIOC_S_FMT (v4l2_format) — 设置格式
///   VIDIOC_REQBUFS / VIDIOC_QUERYBUF / mmap / VIDIOC_QBUF — 缓冲区管理（mmap 零拷贝）
///   VIDIOC_STREAMON / VIDIOC_DQBUF / VIDIOC_STREAMOFF — 采集控制
///   open/close/ioctl/mmap/munmap — 系统调用（libc P/Invoke）
///
/// 当前实现：Linux 下枚举 /dev/video* 设备文件存在性并返回基本信息，
/// CreateCaptureDevice 抛 NotSupportedException 并提示需完整 ioctl 层实现。
/// </remarks>
public class V4L2Backend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "V4L2";

    /// <summary>枚举视频捕获设备</summary>
    /// <returns>Linux 下返回 /dev/video* 设备列表，其他平台返回空</returns>
    public VideoDeviceInfo[] EnumerateDevices()
    {
#if NET5_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var devices = new List<VideoDeviceInfo>();
            for (var i = 0; i < 10; i++)
            {
                var path = $"/dev/video{i}";
                if (File.Exists(path))
                {
                    // 完整实现需 VIDIOC_QUERYCAP 获取真实设备名称和驱动信息
                    devices.Add(new VideoDeviceInfo
                    {
                        Name = $"V4L2 Video{i}",
                        DeviceId = path,
                        BackendType = "V4L2",
                        // SupportedFormats 需 VIDIOC_ENUM_FMT 枚举
                    });
                }
            }
            return devices.ToArray();
        }
#endif
        return [];
    }

    /// <summary>创建视频捕获设备</summary>
    /// <remarks>需完整 ioctl 层实现（mmap 零拷贝缓冲区管理）</remarks>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        throw new NotSupportedException("V4L2 后端需完整 ioctl/mmap 层实现。当前支持设备枚举，采集功能待平台原生 API 对接。");
    }
}
