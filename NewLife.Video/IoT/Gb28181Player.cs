using NewLife.Data;
using System.Net.Sockets;
using System.Text;

namespace NewLife.Video.IoT;

/// <summary>GB/T 28181 实时点播客户端。通过 SIP INVITE 拉取视频流</summary>
/// <remarks>
/// 流程：SIP INVITE (携带 SDP) → 200 OK (设备 SDP) → ACK → RTP 视频流 → BYE
/// </remarks>
public class Gb28181Player : IDisposable
{
    private readonly String _serverIp;
    private readonly Int32 _serverPort;
    private readonly String _deviceId;
    private readonly String _domain;
    private UdpClient _rtpClient;
    private Int32 _cSeq;
    private String _callId;
    private Boolean _playing;
    private Boolean _disposed;

    /// <summary>设备 ID</summary>
    public String DeviceId => _deviceId;

    /// <summary>是否正在播放</summary>
    public Boolean IsPlaying => _playing;

    /// <summary>创建国标点播客户端</summary>
    /// <param name="serverIp">SIP 服务器 IP</param>
    /// <param name="serverPort">SIP 服务器端口</param>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="domain">SIP 域</param>
    public Gb28181Player(String serverIp, Int32 serverPort, String deviceId, String domain)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;
        _deviceId = deviceId;
        _domain = domain;
    }

    /// <summary>开始拉流</summary>
    /// <param name="rtpPort">本地 RTP 接收端口</param>
    /// <returns>是否成功</returns>
    public async Task<Boolean> PlayAsync(Int32 rtpPort = 6000)
    {
        _callId = $"{Guid.NewGuid():N}@{_domain}";

        // 构建 SDP offer
        var sdp = BuildLocalSdp(rtpPort);

        // SIP INVITE
        using var udp = new UdpClient();
        var request = BuildInviteRequest(_callId, sdp);
        var data = Encoding.UTF8.GetBytes(request);
        await udp.SendAsync(data, data.Length, _serverIp, _serverPort);

        // 接收 200 OK
        udp.Client.ReceiveTimeout = 5000;
        var response = await udp.ReceiveAsync();
        var responseText = Encoding.UTF8.GetString(response.Buffer);

        if (!responseText.Contains("200 OK")) return false;

        // ACK
        var ack = BuildAckRequest(_callId);
        var ackData = Encoding.UTF8.GetBytes(ack);
        await udp.SendAsync(ackData, ackData.Length, _serverIp, _serverPort);

        // 开始 RTP 接收
        _rtpClient = new UdpClient(rtpPort);
        _playing = true;

        return true;
    }

    /// <summary>接收一个视频帧</summary>
    /// <returns>视频数据包</returns>
    public async Task<VPacket> ReadFrameAsync()
    {
        if (!_playing || _rtpClient == null) return null;

        try
        {
            _rtpClient.Client.ReceiveTimeout = 3000;
            var result = await _rtpClient.ReceiveAsync();
            return new VPacket
            {
                Data = new Packet(result.Buffer),
                CodecType = VideoTypes.H264,
                PTS = DateTime.Now.Ticks / 10000,
            };
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>停止播放</summary>
    public async Task StopAsync()
    {
        _playing = false;
        _rtpClient?.Close();

        using var udp = new UdpClient();
        var bye = BuildByeRequest(_callId);
        var data = Encoding.UTF8.GetBytes(bye);
        await udp.SendAsync(data, data.Length, _serverIp, _serverPort);
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _rtpClient?.Dispose();
    }

    private String BuildLocalSdp(Int32 rtpPort) =>
        "v=0\r\n" +
        $"o={_deviceId} 0 0 IN IP4 {_serverIp}\r\n" +
        "s=Play\r\n" +
        "c=IN IP4 0.0.0.0\r\n" +
        "t=0 0\r\n" +
        $"m=video {rtpPort} RTP/AVP 96\r\n" +
        "a=rtpmap:96 H264/90000\r\n" +
        "a=recvonly\r\n";

    private String BuildInviteRequest(String callId, String sdp)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"INVITE sip:{_deviceId}@{_domain} SIP/2.0");
        sb.AppendLine($"Via: SIP/2.0/UDP {_serverIp}:{_serverPort};branch=z9hG4bK{Guid.NewGuid():N}");
        sb.AppendLine($"From: <sip:{_deviceId}@{_domain}>;tag={Guid.NewGuid():N}");
        sb.AppendLine($"To: <sip:{_deviceId}@{_domain}>");
        sb.AppendLine($"Call-ID: {callId}");
        sb.AppendLine($"CSeq: {_cSeq++} INVITE");
        sb.AppendLine($"Contact: <sip:{_deviceId}@{_serverIp}:{_serverPort}>");
        sb.AppendLine("Max-Forwards: 70");
        sb.AppendLine("Subject: Play");
        sb.AppendLine("Content-Type: application/sdp");
        sb.AppendLine($"Content-Length: {sdp.Length}");
        sb.AppendLine();
        sb.Append(sdp);
        return sb.ToString();
    }

    private String BuildAckRequest(String callId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ACK sip:{_deviceId}@{_domain} SIP/2.0");
        sb.AppendLine($"Via: SIP/2.0/UDP {_serverIp}:{_serverPort};branch=z9hG4bK{Guid.NewGuid():N}");
        sb.AppendLine($"From: <sip:{_deviceId}@{_domain}>;tag={Guid.NewGuid():N}");
        sb.AppendLine($"To: <sip:{_deviceId}@{_domain}>");
        sb.AppendLine($"Call-ID: {callId}");
        sb.AppendLine($"CSeq: {_cSeq} ACK");
        sb.AppendLine($"Content-Length: 0");
        sb.AppendLine();
        return sb.ToString();
    }

    private String BuildByeRequest(String callId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"BYE sip:{_deviceId}@{_domain} SIP/2.0");
        sb.AppendLine($"Via: SIP/2.0/UDP {_serverIp}:{_serverPort};branch=z9hG4bK{Guid.NewGuid():N}");
        sb.AppendLine($"From: <sip:{_deviceId}@{_domain}>;tag={Guid.NewGuid():N}");
        sb.AppendLine($"To: <sip:{_deviceId}@{_domain}>");
        sb.AppendLine($"Call-ID: {callId}");
        sb.AppendLine($"CSeq: {_cSeq++} BYE");
        sb.AppendLine($"Content-Length: 0");
        sb.AppendLine();
        return sb.ToString();
    }
}
