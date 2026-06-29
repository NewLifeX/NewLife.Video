using System.Net.Sockets;
using System.Text;

namespace NewLife.Video.IoT;

/// <summary>GB/T 28181 设备注册器。实现 SIP 注册与心跳</summary>
/// <remarks>
/// 国标 GB/T 28181 使用 SIP 协议进行设备注册、心跳保活和注销。
/// SIP 服务器默认端口 5060 (UDP)。
/// </remarks>
public class Gb28181Registrar
{
    private readonly String _serverIp;
    private readonly Int32 _serverPort;
    private readonly String _deviceId;
    private readonly String _domain;
    private readonly UdpClient _udpClient;
    private Int32 _cSeq;
    private Boolean _registered;

    /// <summary>设备 ID (20位国标编码)</summary>
    public String DeviceId => _deviceId;

    /// <summary>是否已注册</summary>
    public Boolean IsRegistered => _registered;

    /// <summary>创建国标注册器</summary>
    /// <param name="serverIp">SIP 服务器 IP</param>
    /// <param name="serverPort">SIP 服务器端口</param>
    /// <param name="deviceId">设备 20 位国标编码</param>
    /// <param name="domain">SIP 域</param>
    public Gb28181Registrar(String serverIp, Int32 serverPort, String deviceId, String domain)
    {
        _serverIp = serverIp ?? throw new ArgumentNullException(nameof(serverIp));
        _serverPort = serverPort;
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _domain = domain ?? serverIp;
        _udpClient = new UdpClient();
        _cSeq = 1;
    }

    /// <summary>发送注册请求</summary>
    /// <param name="expires">过期时间（秒，默认 3600）</param>
    public async Task<Boolean> RegisterAsync(Int32 expires = 3600)
    {
        var callId = $"{Guid.NewGuid():N}@{_domain}";
        var request = BuildSipRequest("REGISTER", callId, $"Expires: {expires}");
        var response = await SendUdpAsync(request);

        _registered = response.Contains("200 OK");
        return _registered;
    }

    /// <summary>发送心跳保活</summary>
    public async Task<Boolean> KeepAliveAsync()
    {
        return await RegisterAsync(3600);
    }

    /// <summary>注销</summary>
    public async Task<Boolean> UnregisterAsync()
    {
        var result = await RegisterAsync(0);
        _registered = false;
        return result;
    }

    private String BuildSipRequest(String method, String callId, String extraHeader = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{method} sip:{_deviceId}@{_domain} SIP/2.0");
        sb.AppendLine($"Via: SIP/2.0/UDP {_serverIp}:{_serverPort};rport;branch=z9hG4bK{Guid.NewGuid():N}");
        sb.AppendLine($"From: <sip:{_deviceId}@{_domain}>;tag={Guid.NewGuid():N}");
        sb.AppendLine($"To: <sip:{_deviceId}@{_domain}>");
        sb.AppendLine($"Call-ID: {callId}");
        sb.AppendLine($"CSeq: {_cSeq++} {method}");
        sb.AppendLine($"Contact: <sip:{_deviceId}@{_serverIp}:{_serverPort}>");
        sb.AppendLine("Max-Forwards: 70");
        sb.AppendLine("User-Agent: NewLife.Video GB28181 Registrar");
        if (extraHeader != null) sb.AppendLine(extraHeader);
        sb.AppendLine($"Content-Length: 0");
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task<String> SendUdpAsync(String message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _udpClient.SendAsync(data, data.Length, _serverIp, _serverPort);

        try
        {
            _udpClient.Client.ReceiveTimeout = 3000;
            var result = await _udpClient.ReceiveAsync();
            return Encoding.UTF8.GetString(result.Buffer);
        }
        catch (SocketException)
        {
            return "";
        }
    }
}
