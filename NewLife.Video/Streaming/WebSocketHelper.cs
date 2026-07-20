using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NewLife.Video.Streaming;

/// <summary>WebSocket 握手辅助类。在 TcpClient 上执行 RFC 6455 握手</summary>
internal static class WebSocketHelper
{
    private const String MagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>从 TcpClient 执行 WebSocket 握手，返回已升级的网络流</summary>
    /// <param name="tcpClient">已连接的 TcpClient</param>
    /// <param name="receiveTimeout">接收握手请求超时（毫秒）</param>
    /// <returns>握手成功返回 true</returns>
    public static Boolean PerformHandshake(TcpClient tcpClient, Int32 receiveTimeout = 5000)
    {
        if (tcpClient == null) throw new ArgumentNullException(nameof(tcpClient));

        var stream = tcpClient.GetStream();
        stream.ReadTimeout = receiveTimeout;

        // 读取 HTTP 升级请求
        var requestBuilder = new StringBuilder();
        var buffer = new Byte[4096];
        var totalRead = 0;

        while (true)
        {
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0) return false;

            requestBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            totalRead += bytesRead;

            // HTTP 请求头以 \r\n\r\n 结束
            if (requestBuilder.ToString().Contains("\r\n\r\n"))
                break;
        }

        var request = requestBuilder.ToString();

        // 解析请求行
        var lines = request.Split("\r\n");
        if (lines.Length < 1) return false;

        var requestLine = lines[0];
        if (!requestLine.StartsWith("GET "))
            return false;

        // 解析头
        var headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (String.IsNullOrEmpty(line)) break;

            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var name = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                headers[name] = value;
            }
        }

        // 验证 WebSocket 升级
        if (!headers.TryGetValue("Upgrade", out var upgrade) ||
            !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!headers.TryGetValue("Sec-WebSocket-Key", out var webSocketKey) ||
            String.IsNullOrEmpty(webSocketKey))
            return false;

        // 计算 Sec-WebSocket-Accept
        var acceptKey = ComputeAcceptKey(webSocketKey);

        // 发送 101 响应
        var response = new StringBuilder();
        response.Append("HTTP/1.1 101 Switching Protocols\r\n");
        response.Append("Upgrade: websocket\r\n");
        response.Append("Connection: Upgrade\r\n");
        response.Append($"Sec-WebSocket-Accept: {acceptKey}\r\n");
        response.Append("\r\n");

        var responseBytes = Encoding.ASCII.GetBytes(response.ToString());
        stream.Write(responseBytes, 0, responseBytes.Length);
        stream.Flush();

        return true;
    }

    /// <summary>计算 WebSocket Accept Key</summary>
    /// <param name="webSocketKey">客户端提供的 Sec-WebSocket-Key</param>
    /// <returns>Base64 编码的 Accept Key</returns>
    public static String ComputeAcceptKey(String webSocketKey)
    {
        var combined = webSocketKey + MagicGuid;
        var data = Encoding.ASCII.GetBytes(combined);
#if NET5_0_OR_GREATER
        var hash = SHA1.HashData(data);
#else
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(data);
#endif
        return Convert.ToBase64String(hash);
    }

    /// <summary>发送 WebSocket 二进制帧（服务器到客户端，无掩码）</summary>
    /// <param name="stream">网络流</param>
    /// <param name="data">数据</param>
    public static void SendBinaryFrame(NetworkStream stream, Byte[] data)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (data == null) throw new ArgumentNullException(nameof(data));

        // 帧头：FIN=1, RSV=0, Opcode=2 (binary)
        var header = new List<Byte> { 0x82 };

        if (data.Length < 126)
        {
            // 7 位长度（无掩码）
            header.Add((Byte)data.Length);
        }
        else if (data.Length <= 0xFFFF)
        {
            // 16 位长度
            header.Add(0x7E);
            header.Add((Byte)((data.Length >> 8) & 0xFF));
            header.Add((Byte)(data.Length & 0xFF));
        }
        else
        {
            // 64 位长度
            header.Add(0x7F);
            for (var i = 56; i >= 0; i -= 8)
                header.Add((Byte)((data.Length >> i) & 0xFF));
        }

        stream.Write([.. header], 0, header.Count);
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
}
