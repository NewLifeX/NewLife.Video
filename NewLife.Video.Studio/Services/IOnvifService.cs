using NewLife.Video.IoT;

namespace NewLife.Video.Studio.Services;

/// <summary>ONVIF 设备服务接口（M5-2）</summary>
public interface IOnvifService
{
    /// <summary>发现 ONVIF 设备</summary>
    /// <param name="timeout">超时时间（秒）</param>
    /// <returns>发现的设备信息列表</returns>
    Task<OnvifDeviceInfo[]> DiscoverDevicesAsync(Int32 timeout = 5);

    /// <summary>获取设备媒体流 URI</summary>
    String? GetStreamUri(String deviceAddress, String username, String password);
}
