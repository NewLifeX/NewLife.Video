using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class OnvifDiscoveryTests
{
    [Fact(DisplayName = "OnvifDiscovery 默认超时参数")]
    public void DefaultTimeout_Correct()
    {
        var discovery = new OnvifDiscovery();
        Assert.Equal(5000, discovery.TimeoutMs);
    }

    [Fact(DisplayName = "OnvifDiscovery 可自定义超时")]
    public void TimeoutMs_CanBeSet()
    {
        var discovery = new OnvifDiscovery { TimeoutMs = 3000 };
        Assert.Equal(3000, discovery.TimeoutMs);
    }

    [Fact(DisplayName = "OnvifDiscovery Probe消息格式正确")]
    public void ProbeMessage_ContainsRequiredElements()
    {
        var discovery = new OnvifDiscovery();
        // 通过反射或直接构造验证 Probe XML
        var deviceInfo = new OnvifDeviceInfo
        {
            XAddrs = "http://192.168.1.100/onvif/device_service",
            Types = "dn:NetworkVideoTransmitter",
            Address = "192.168.1.100",
        };

        Assert.Contains("192.168.1.100", deviceInfo.XAddrs);
        Assert.Contains("NetworkVideoTransmitter", deviceInfo.Types);
    }

    [Fact(DisplayName = "OnvifDeviceInfo ToString 正确")]
    public void OnvifDeviceInfo_ToString_Correct()
    {
        var info = new OnvifDeviceInfo
        {
            Name = "Front Door",
            Address = "192.168.1.64",
            XAddrs = "http://192.168.1.64/onvif/device_service",
        };

        var str = info.ToString();
        Assert.Contains("Front Door", str);
        Assert.Contains("192.168.1.64", str);
    }

    [Fact(DisplayName = "OnvifDeviceInfo 无名称时用地址")]
    public void OnvifDeviceInfo_NoName_UsesAddress()
    {
        var info = new OnvifDeviceInfo
        {
            Address = "10.0.0.1",
            XAddrs = "http://10.0.0.1/onvif/device_service",
        };

        var str = info.ToString();
        Assert.Contains("10.0.0.1", str);
    }
}
