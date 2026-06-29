using System.Net;
using System.Net.Sockets;
using System.Text;
using NewLife.Data;

namespace NewLife.Video.IoT;

/// <summary>ONVIF 设备发现。通过 WS-Discovery 协议 (UDP 组播) 发现局域网内的 ONVIF 摄像头</summary>
/// <remarks>
/// WS-Discovery 使用 SOAP over UDP，组播地址 239.255.255.250:3702。
/// Probe 消息用于发现设备，ProbeMatch 响应包含设备 XAddrs（服务地址）。
/// </remarks>
public class OnvifDiscovery
{
    private const String MulticastAddress = "239.255.255.250";
    private const Int32 MulticastPort = 3702;

    /// <summary>发现超时时间（毫秒）</summary>
    public Int32 TimeoutMs { get; set; } = 5000;

    /// <summary>发现局域网内的 ONVIF 设备</summary>
    /// <returns>发现的设备列表</returns>
    public async Task<OnvifDeviceInfo[]> DiscoverAsync()
    {
        var devices = new List<OnvifDeviceInfo>();
        var probeMsg = BuildProbeMessage();

        using var client = new UdpClient();
        client.EnableBroadcast = true;
        client.Client.SendTimeout = TimeoutMs;
        client.Client.ReceiveTimeout = TimeoutMs;

        // 发送 Probe 到组播地址
        var probeBytes = Encoding.UTF8.GetBytes(probeMsg);
        var endPoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
        await client.SendAsync(probeBytes, probeBytes.Length, endPoint);

        // 接收 ProbeMatch 响应
        var deadline = DateTime.Now.AddMilliseconds(TimeoutMs);
        while (DateTime.Now < deadline)
        {
            try
            {
                var result = await client.ReceiveAsync();
                var response = Encoding.UTF8.GetString(result.Buffer);

                var device = ParseProbeMatch(response);
                if (device != null && !devices.Any(d => d.XAddrs == device.XAddrs))
                    devices.Add(device);
            }
            catch (SocketException)
            {
                break; // 超时，正常退出
            }
        }

        return devices.ToArray();
    }

    /// <summary>同步发现设备</summary>
    public OnvifDeviceInfo[] Discover()
    {
        return DiscoverAsync().GetAwaiter().GetResult();
    }

    /// <summary>构建 WS-Discovery Probe 消息</summary>
    private static String BuildProbeMessage()
    {
        var messageId = $"urn:uuid:{Guid.NewGuid()}";
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope""
               xmlns:wsa=""http://schemas.xmlsoap.org/ws/2004/08/addressing""
               xmlns:wsd=""http://schemas.xmlsoap.org/ws/2005/04/discovery""
               xmlns:dn=""http://www.onvif.org/ver10/network/wsdl"">
  <soap:Header>
    <wsa:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</wsa:Action>
    <wsa:MessageID>{messageId}</wsa:MessageID>
    <wsa:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</wsa:To>
  </soap:Header>
  <soap:Body>
    <wsd:Probe>
      <wsd:Types>dn:NetworkVideoTransmitter</wsd:Types>
    </wsd:Probe>
  </soap:Body>
</soap:Envelope>";
    }

    /// <summary>解析 ProbeMatch 响应</summary>
    private static OnvifDeviceInfo ParseProbeMatch(String xml)
    {
        if (String.IsNullOrEmpty(xml)) return null;

        var info = new OnvifDeviceInfo();

        // 提取 XAddrs
        var xAddrsStart = xml.IndexOf("<wsd:XAddrs>", StringComparison.Ordinal);
        if (xAddrsStart > 0)
        {
            xAddrsStart += "<wsd:XAddrs>".Length;
            var xAddrsEnd = xml.IndexOf("</wsd:XAddrs>", xAddrsStart, StringComparison.Ordinal);
            if (xAddrsEnd > xAddrsStart)
                info.XAddrs = xml.Substring(xAddrsStart, xAddrsEnd - xAddrsStart).Trim();
        }

        // 提取设备名称 (Types)
        var typesStart = xml.IndexOf("<wsd:Types>", StringComparison.Ordinal);
        if (typesStart > 0)
        {
            typesStart += "<wsd:Types>".Length;
            var typesEnd = xml.IndexOf("</wsd:Types>", typesStart, StringComparison.Ordinal);
            if (typesEnd > typesStart)
                info.Types = xml.Substring(typesStart, typesEnd - typesStart).Trim();
        }

        // 提取设备地址 (从 XAddrs 解析)
        if (!String.IsNullOrEmpty(info.XAddrs))
        {
            var addrStart = info.XAddrs.IndexOf("://", StringComparison.Ordinal);
            if (addrStart > 0)
            {
                addrStart += 3;
                var addrEnd = info.XAddrs.IndexOf('/', addrStart);
                if (addrEnd > addrStart)
                    info.Address = info.XAddrs.Substring(addrStart, addrEnd - addrStart);
                else
                    info.Address = info.XAddrs.Substring(addrStart);
            }
        }

        // 提取 Scope
        var scopeStart = xml.IndexOf("<wsd:Scopes>", StringComparison.Ordinal);
        if (scopeStart > 0)
        {
            scopeStart += "<wsd:Scopes>".Length;
            var scopeEnd = xml.IndexOf("</wsd:Scopes>", scopeStart, StringComparison.Ordinal);
            if (scopeEnd > scopeStart)
                info.Scopes = xml.Substring(scopeStart, scopeEnd - scopeStart).Trim();
        }

        // 有 XAddrs 才认为是有效设备
        return String.IsNullOrEmpty(info.XAddrs) ? null : info;
    }
}

/// <summary>ONVIF 设备信息</summary>
public class OnvifDeviceInfo
{
    /// <summary>设备服务地址</summary>
    public String XAddrs { get; set; }

    /// <summary>设备类型</summary>
    public String Types { get; set; }

    /// <summary>设备 IP 地址</summary>
    public String Address { get; set; }

    /// <summary>设备作用域</summary>
    public String Scopes { get; set; }

    /// <summary>设备名称（从 Scopes 或 Types 提取）</summary>
    public String Name { get; set; }

    /// <summary>字符串表示</summary>
    public override String ToString() => $"{Name ?? Address ?? "Unknown"} [{XAddrs}]";
}
