using System.Runtime.InteropServices;

namespace NewLife.Video.Devices;

/// <summary>macOS AVFoundation 视频后端骨架。通过 AVFoundation 框架枚举和采集摄像头</summary>
/// <remarks>
/// AVFoundation 是 Apple 平台（macOS/iOS/tvOS）的标准音视频采集框架。
///
/// 完整实现需要的概念与 API 层：
///   AVCaptureDevice — 设备发现与属性查询
///   AVCaptureDeviceInput — 将设备接入会话
///   AVCaptureVideoDataOutput — 逐帧回调输出
///   AVCaptureSession — 会话管理与启停控制
///   CMSampleBuffer / CVPixelBuffer — 帧数据封装
///
/// .NET 调用路径：
///   1. Xamarin.Mac / .NET MAUI — 官方绑定，直接调用 AVFoundation API
///   2. Objective-C P/Invoke — 通过 libobjc.dylib 动态调用（objc_msgSend）
///   3. AVFoundation 命令行工具 — 使用 avcapture 等第三方 CLI 工具
///
/// 当前实现：macOS 下返回占位设备信息，
/// CreateCaptureDevice 抛出 NotSupportedException 并提示需 .NET MAUI 或 P/Invoke 层实现。
/// </remarks>
public class AVFoundationBackend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "AVFoundation";

    /// <summary>枚举视频捕获设备</summary>
    /// <returns>macOS 下返回占位设备列表，其他平台返回空</returns>
    public VideoDeviceInfo[] EnumerateDevices()
    {
#if NET5_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // 完整实现需通过 AVCaptureDevice.devices() 或
            // AVCaptureDevice.DiscoverySession 枚举真实摄像头设备
            var devices = new List<VideoDeviceInfo>
            {
                new()
                {
                    Name = "FaceTime HD Camera",
                    DeviceId = "avfoundation://default",
                    BackendType = "AVFoundation",
                    // SupportedFormats 需查询 AVCaptureDevice.formats
                },
                new()
                {
                    Name = "External USB Camera",
                    DeviceId = "avfoundation://external",
                    BackendType = "AVFoundation",
                },
            };
            return devices.ToArray();
        }
#endif
        return [];
    }

    /// <summary>创建视频捕获设备</summary>
    /// <remarks>
    /// 需 .NET MAUI (Microsoft.macOS) 或 Xamarin.Mac 提供 AVFoundation 绑定，
    /// 或通过 P/Invoke objc_msgSend 动态调用原生 API。
    /// 核心流程：AVCaptureSession → addInput(AVCaptureDeviceInput) → addOutput(AVCaptureVideoDataOutput) → startRunning
    /// </remarks>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        throw new NotSupportedException(
            "AVFoundation 后端需 .NET MAUI (macOS) 或 Xamarin.Mac 绑定支持。\n" +
            "当前为骨架实现，采集功能待平台原生 API 对接。\n" +
            "实现路径：P/Invoke objc_msgSend 调用 AVCaptureSession/AVCaptureDevice API，\n" +
            "或使用 .NET MAUI 的 Microsoft.macOS 包中的 AVFoundation 绑定。");
    }
}
