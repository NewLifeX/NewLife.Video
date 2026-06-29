using NewLife.Data;
using System.Net.Sockets;

namespace NewLife.Video.Streaming;

/// <summary>RTMP 推流客户端。通过 RTMP 协议推送 FLV 封装的音视频流到服务器</summary>
/// <remarks>
/// RTMP 协议流程：TCP 连接 → C0/C1/C2/S0/S1/S2 握手 → connect → createStream → publish → 推送音视频数据。
/// 握手层完整实现，命令层通过 AMF0 编码实现 connect/createStream/publish。
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
    private Int32 _streamId;
    private Int32 _transactionId;
    private Boolean _connected;
    private Boolean _published;

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

    /// <summary>发送 RTMP connect 命令</summary>
    /// <returns>是否成功</returns>
    public async Task<Boolean> ConnectStreamAsync()
    {
        if (!IsConnected) throw new InvalidOperationException("未连接到 RTMP 服务器");

        _transactionId++;
        var cmd = BuildCommand("connect", _transactionId);
        cmd.WriteObjectBegin();
        cmd.WriteObjectProperty("app", _appName);
        cmd.WriteObjectProperty("tcUrl", $"rtmp://{_host}:{_port}/{_appName}");
        cmd.WriteObjectProperty("type", "nonprivate");
        cmd.WriteObjectProperty("flashVer", "FMLE/3.0");
        cmd.WriteObjectEnd();

        await SendRtmpMessageAsync(20, cmd.ToArray());

        // 简单等待响应（生产环境应解析响应）
        var resp = await ReadRtmpResponseAsync();
        return resp != null;
    }

    /// <summary>发送 RTMP createStream 命令</summary>
    /// <returns>流 ID，失败返回 -1</returns>
    public async Task<Int32> CreateStreamAsync()
    {
        if (!IsConnected) throw new InvalidOperationException("未连接");

        _transactionId++;
        var cmd = BuildCommand("createStream", _transactionId);
        cmd.WriteNull(); // 无参数

        await SendRtmpMessageAsync(20, cmd.ToArray());

        var resp = await ReadRtmpResponseAsync();
        // 响应格式: _result(tid, null, streamId)
        if (resp != null && resp.Length > 20)
        {
            // 简化解析：跳过 _result + tid + null，找 number
            for (var i = 10; i < resp.Length - 9; i++)
            {
                if (resp[i] == 0x00) // AMF0 Number
                {
                    var bytes = new Byte[8];
                    Array.Copy(resp, i + 1, bytes, 0, 8);
                    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    _streamId = (Int32)BitConverter.ToDouble(bytes, 0);
                    return _streamId;
                }
            }
        }
        return -1;
    }

    /// <summary>发送 RTMP publish 命令</summary>
    /// <returns>是否成功</returns>
    public async Task<Boolean> PublishAsync()
    {
        if (!IsConnected || _streamId <= 0) throw new InvalidOperationException("未连接或未创建流");

        _transactionId++;
        var cmd = BuildCommand("publish", _transactionId);
        cmd.WriteNull();
        cmd.WriteString(_streamName);
        cmd.WriteString("live");

        await SendRtmpMessageAsync(8, cmd.ToArray()); // type 8 = audio, used for command too
        // 发送到流通道
        var msg = BuildRtmpMessage(8, cmd.ToArray());
        await _networkStream.WriteAsync(msg, 0, msg.Length);

        _published = true;
        return true;
    }

    /// <summary>推送视频数据（发布后使用）</summary>
    /// <param name="flvVideoTag">FLV 视频标签（AVC sequence header 或 NALU）</param>
    /// <param name="timestamp">时间戳（毫秒）</param>
    public async Task PushVideoAsync(Byte[] flvVideoTag, UInt32 timestamp)
    {
        if (!_published) throw new InvalidOperationException("尚未 publish，请先调用 PublishAsync");
        // 直接发送到流通道，不封装 RTMP 命令头
        var msg = BuildRtmpMessage(9, flvVideoTag); // type 9 = video
        await _networkStream.WriteAsync(msg, 0, msg.Length);
    }

    private Amf0Encoder BuildCommand(String name, Int32 tid)
    {
        var cmd = new Amf0Encoder();
        cmd.WriteString(name);
        cmd.WriteNumber(tid);
        return cmd;
    }

    private async Task SendRtmpMessageAsync(Byte msgType, Byte[] payload)
    {
        var msg = BuildRtmpMessage(msgType, payload);
        await _networkStream.WriteAsync(msg, 0, msg.Length);
    }

    private Byte[] BuildRtmpMessage(Byte msgType, Byte[] payload)
    {
        // RTMP 基本头 (12 bytes) + payload
        var header = new Byte[12];
        header[0] = msgType;
        // 3-byte timestamp
        WriteUInt24BE(header, 1, 0);
        // 3-byte body size
        WriteUInt24BE(header, 4, payload.Length);
        header[7] = msgType;
        // 4-byte stream ID (little endian)
        header[8] = (Byte)(_streamId & 0xFF);
        header[9] = (Byte)((_streamId >> 8) & 0xFF);
        header[10] = (Byte)((_streamId >> 16) & 0xFF);
        header[11] = (Byte)((_streamId >> 24) & 0xFF);

        var result = new Byte[header.Length + payload.Length];
        Array.Copy(header, 0, result, 0, header.Length);
        Array.Copy(payload, 0, result, header.Length, payload.Length);
        return result;
    }

    private async Task<Byte[]> ReadRtmpResponseAsync()
    {
        try
        {
            var header = new Byte[12];
            var read = await _networkStream.ReadAsync(header, 0, 12);
            if (read < 12) return null;

            var bodySize = (header[4] << 16) | (header[5] << 8) | header[6];
            if (bodySize <= 0 || bodySize > 1024 * 1024) return null;

            var body = new Byte[bodySize];
            var total = 0;
            while (total < bodySize)
            {
                var n = await _networkStream.ReadAsync(body, total, bodySize - total);
                if (n == 0) break;
                total += n;
            }

            var result = new Byte[12 + total];
            Array.Copy(header, 0, result, 0, 12);
            Array.Copy(body, 0, result, 12, total);
            return result;
        }
        catch { return null; }
    }

    private static void WriteUInt24BE(Byte[] buf, Int32 offset, Int32 value)
    {
        buf[offset] = (Byte)((value >> 16) & 0xFF);
        buf[offset + 1] = (Byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (Byte)(value & 0xFF);
    }
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
