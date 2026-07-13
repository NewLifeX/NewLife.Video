using NewLife.Log;
using NewLife.Video.IoT;

namespace NewLife.Video.Studio.Services;

/// <summary>ONVIF 设备服务</summary>
public class OnvifService : IOnvifService
{
    /// <summary>发现 ONVIF 设备</summary>
    public async Task<OnvifDeviceInfo[]> DiscoverDevicesAsync(Int32 timeout = 5)
    {
        try
        {
            var discovery = new OnvifDiscovery();
            return await discovery.DiscoverAsync();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return [];
        }
    }

    /// <summary>获取设备媒体流 URI</summary>
    public String? GetStreamUri(String deviceAddress, String username, String password)
    {
        try
        {
            var client = new OnvifMediaClient(deviceAddress, username, password);
            var profiles = client.GetProfiles();
            if (profiles == null || profiles.Length == 0) return null;

            var uri = client.GetStreamUriAsync(profiles[0].Token);
            return uri?.Result?.ToString();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return null;
        }
    }
}
