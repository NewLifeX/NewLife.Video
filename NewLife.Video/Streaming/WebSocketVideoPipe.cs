using NewLife.Buffers;
using NewLife.Data;
using System.Net.WebSockets;
using System.Text;

namespace NewLife.Video.Streaming;

/// <summary>WebSocket 视频帧双向通道。通过 WebSocket 传输视频帧数据</summary>
/// <remarks>
/// 支持发送/接收视频帧（VPacket 格式），内置简单的帧边界协议。
/// 帧格式：[4字节长度(BE)] + [VPacket JSON/二进制数据]
/// </remarks>
public class WebSocketVideoPipe
{
    private readonly WebSocket _socket;

    /// <summary>底层 WebSocket</summary>
    public WebSocket Socket => _socket;

    /// <summary>创建 WebSocket 视频管道</summary>
    /// <param name="socket">已连接的 WebSocket</param>
    public WebSocketVideoPipe(WebSocket socket)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    /// <summary>发送视频数据包</summary>
    /// <param name="packet">视频数据包</param>
    public async Task SendAsync(VPacket packet)
    {
        if (packet?.Data == null) return;

        var lengthPrefix = new Byte[4];
        var lw = new SpanWriter(lengthPrefix) { IsLittleEndian = false };

        if (packet.Data.TryGetArray(out var seg))
        {
            lw.Write(seg.Count);
            await _socket.SendAsync(new ArraySegment<Byte>(lengthPrefix), WebSocketMessageType.Binary, false, CancellationToken.None);
            await _socket.SendAsync(new ArraySegment<Byte>(seg.Array, seg.Offset, seg.Count), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        else
        {
            var data = packet.Data.ToArray();
            lw.Write(data.Length);
            await _socket.SendAsync(new ArraySegment<Byte>(lengthPrefix), WebSocketMessageType.Binary, false, CancellationToken.None);
            await _socket.SendAsync(new ArraySegment<Byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }

    /// <summary>接收视频数据包</summary>
    /// <returns>视频数据包，连接关闭时返回 null</returns>
    public async Task<VPacket> ReceiveAsync()
    {
        // 读取长度前缀
        var lengthBuffer = new Byte[4];
        var result = await _socket.ReceiveAsync(new ArraySegment<Byte>(lengthBuffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) return null;

        var lr = new SpanReader(lengthBuffer) { IsLittleEndian = false };
        var dataLength = (Int32)lr.ReadUInt32();
        if (dataLength <= 0 || dataLength > 100 * 1024 * 1024) return null; // 最大 100MB

        // 读取数据
        var data = new Byte[dataLength];
        var offset = 0;
        while (offset < dataLength)
        {
            var remaining = dataLength - offset;
            var segLength = Math.Min(remaining, 8192);
            var segResult = await _socket.ReceiveAsync(new ArraySegment<Byte>(data, offset, segLength), CancellationToken.None);
            offset += segResult.Count;
            if (segResult.EndOfMessage) break;
        }

        return new VPacket
        {
            Data = new ArrayPacket(data, 0, dataLength),
            CodecType = VideoTypes.Unknown,
        };
    }

    /// <summary>发送文本消息（用于信令）</summary>
    /// <param name="message">文本消息</param>
    public async Task SendTextAsync(String message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(new ArraySegment<Byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>接收文本消息</summary>
    /// <returns>文本消息</returns>
    public async Task<String> ReceiveTextAsync()
    {
        var buffer = new Byte[8192];
        var result = await _socket.ReceiveAsync(new ArraySegment<Byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) return null;
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
