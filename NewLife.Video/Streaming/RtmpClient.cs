using NewLife.Data;
using System.Net.Sockets;

namespace NewLife.Video.Streaming;

/// <summary>RTMP 推流客户端。通过 RTMP 协议推送 FLV 封装的音视频流到服务器</summary>
/// <remarks>
/// RTMP 协议流程：TCP 连接 → C0/C1/C2/S0/S1/S2 握手 → connect → createStream → publish → 推送音视频数据。
/// 当前为骨架实现，提供 RTMP URL 解析和基本连接结构。
/// 完整握手和数据推送在后续版本中完善。
/// </remarks>
public class RtmpClient : IDisposable
{
    private String _url;
    private String _host;
    private Int32 _port;
    private String _appName;
    private String _streamName;
    private System.Net.Sockets.TcpClient _tcpClient;
    private NetworkStream _networkStream;

    /// <summary>RTMP URL</summary>
    public String Url => _url;

    /// <summary>主机名</summary>
    public String Host => _host;

    /// <summary>端口</summary>
    public Int32 Port => _port;

    /// <summary>应用名</summary>
    public String AppName => _appName;

    /// <summary>流名</summary>
    public String StreamName => _streamName;

    /// <summary>是否已连接</summary>
    public Boolean IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>创建 RTMP 客户端</summary>
    /// <param name="url">RTMP URL（如 rtmp://192.168.1.100/live/stream）</param>
    public RtmpClient(String url)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
        ParseUrl();
    }

    /// <summary>解析 RTMP URL</summary>
    private void ParseUrl()
    {
        if (!_url.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("RTMP URL 必须以 rtmp:// 开头");

        var uri = _url.Substring(7); // 去掉 "rtmp://"
        var slashIdx = uri.IndexOf('/');

        if (slashIdx < 0)
            throw new ArgumentException("RTMP URL 格式错误: rtmp://host[:port]/app/stream");

        var hostPart = uri.Substring(0, slashIdx);
        var pathPart = uri.Substring(slashIdx + 1);

        // 解析主机和端口
        var colonIdx = hostPart.IndexOf(':');
        if (colonIdx > 0)
        {
            _host = hostPart.Substring(0, colonIdx);
            _port = Int32.Parse(hostPart.Substring(colonIdx + 1));
        }
        else
        {
            _host = hostPart;
            _port = 1935; // 默认 RTMP 端口
        }

        // 解析应用名和流名
        var lastSlash = pathPart.LastIndexOf('/');
        if (lastSlash > 0)
        {
            _appName = pathPart.Substring(0, lastSlash);
            _streamName = pathPart.Substring(lastSlash + 1);
        }
        else
        {
            _appName = pathPart;
            _streamName = "";
        }
    }

    /// <summary>连接到 RTMP 服务器</summary>
    /// <returns>是否连接成功</returns>
    public async Task<Boolean> ConnectAsync()
    {
        if (IsConnected) return true;

        try
        {
            _tcpClient = new System.Net.Sockets.TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            _networkStream = _tcpClient.GetStream();

            // RTMP 握手：发送 C0 + C1
            await SendHandshakeAsync();

            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    /// <summary>发送 RTMP 握手（C0 + C1）</summary>
    private async Task SendHandshakeAsync()
    {
        // C0: 协议版本 (1 byte) = 3
        _networkStream.WriteByte(0x03);

        // C1: 时间戳(4) + 零(4) + 随机数据(1528) = 1536 bytes
        var c1 = new Byte[1536];
        // 时间戳（当前毫秒）
        var ts = (UInt32)Environment.TickCount;
        c1[0] = (Byte)(ts >> 24);
        c1[1] = (Byte)(ts >> 16);
        c1[2] = (Byte)(ts >> 8);
        c1[3] = (Byte)(ts);

        // 随机填充剩余 1528 字节
        var rng = new Random();
        for (var i = 8; i < c1.Length; i++)
            c1[i] = (Byte)rng.Next(256);

        await _networkStream.WriteAsync(c1, 0, c1.Length);

        // 读取 S0 + S1 + S2 (1 + 1536 + 1536 = 3073 bytes)
        var response = new Byte[3073];
        var read = 0;
        while (read < response.Length)
        {
            var n = await _networkStream.ReadAsync(response, read, response.Length - read);
            if (n == 0) throw new IOException("RTMP 握手失败：连接关闭");
            read += n;
        }

        // 发送 C2（回显 S1 数据）
        var c2 = new Byte[1536];
        Array.Copy(response, 1, c2, 0, 1536); // S1 数据
        await _networkStream.WriteAsync(c2, 0, c2.Length);
    }

    /// <summary>推送 FLV 标签数据</summary>
    /// <param name="flvData">FLV 标签数据</param>
    /// <param name="timestamp">时间戳（毫秒）</param>
    /// <param name="isVideo">是否为视频数据</param>
    public async Task PushDataAsync(Byte[] flvData, UInt32 timestamp, Boolean isVideo)
    {
        if (!IsConnected) throw new InvalidOperationException("未连接到 RTMP 服务器");

        // 简化实现：直接发送 FLV tag
        // RTMP 消息头 (11 bytes) + FLV tag 数据
        var messageType = isVideo ? (Byte)9 : (Byte)8;
        var msgHeader = new Byte[11];
        msgHeader[0] = messageType;
        // 3-byte timestamp
        msgHeader[1] = (Byte)((timestamp >> 16) & 0xFF);
        msgHeader[2] = (Byte)((timestamp >> 8) & 0xFF);
        msgHeader[3] = (Byte)(timestamp & 0xFF);
        // 3-byte data length
        var dataLen = flvData.Length;
        msgHeader[4] = (Byte)((dataLen >> 16) & 0xFF);
        msgHeader[5] = (Byte)((dataLen >> 8) & 0xFF);
        msgHeader[6] = (Byte)(dataLen & 0xFF);
        // message type (但这里 type 已写)
        msgHeader[7] = messageType;
        // stream ID (4 bytes, default 1)
        msgHeader[8] = 0x01;
        msgHeader[9] = 0x00;
        msgHeader[10] = 0x00;

        await _networkStream.WriteAsync(msgHeader, 0, msgHeader.Length);
        await _networkStream.WriteAsync(flvData, 0, flvData.Length);
    }

    /// <summary>断开连接</summary>
    public void Disconnect()
    {
        _networkStream?.Close();
        _tcpClient?.Close();
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Disconnect();
        _networkStream?.Dispose();
        _tcpClient?.Dispose();
    }
}
