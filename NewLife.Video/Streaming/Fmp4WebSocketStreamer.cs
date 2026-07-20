using System.Net;
using System.Net.Sockets;
using NewLife.Video.Containers;
using NewLife.Video.FMp4;

namespace NewLife.Video.Streaming;

/// <summary>fMP4 over WebSocket 实时播放服务端。支持 RFC 6455 WebSocket 握手协议</summary>
/// <remarks>
/// 接收浏览器 MSE/EME 的 WebSocket 连接，将 fMP4 分段数据推送至客户端。
/// 支持 RTSP→WebSocket 桥接模式：接收 RTSP 摄像头流，转封装为 fMP4 分段，通过 WebSocket 推送。
/// 
/// 使用示例：
/// <code>
/// var streamer = new Fmp4WebSocketStreamer(8080);
/// streamer.Start();
/// // 推送帧数据
/// streamer.PushFrame(vPacket);
/// </code>
/// </remarks>
public class Fmp4WebSocketStreamer : IDisposable
{
    private readonly Int32 _port;
    private TcpListener _listener;
    private readonly List<NetworkStream> _clients = [];
    private readonly Object _lock = new();
    private Boolean _running;

    /// <summary>是否正在运行</summary>
    public Boolean IsRunning => _running;

    /// <summary>已连接的客户端数</summary>
    public Int32 ClientCount { get { lock (_lock) return _clients.Count; } }

    /// <summary>创建 fMP4 WebSocket 流服务端</summary>
    /// <param name="port">监听端口（默认 8080）</param>
    public Fmp4WebSocketStreamer(Int32 port = 8080)
    {
        _port = port;
    }

    /// <summary>启动服务</summary>
    public void Start()
    {
        _running = true;
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    /// <summary>推一帧视频数据（每帧自动封装为 fMP4 分段）</summary>
    /// <param name="frame">视频帧数据包</param>
    public void PushFrame(VPacket frame)
    {
        if (frame?.Data == null) return;

        using var ms = new MemoryStream();
        using (var writer = new Fmp4SegmentWriter(ms, segmentFrames: 1, leaveOpen: true))
        {
            writer.WriteMetadata(1920, 1080, frame.CodecType, 30);
            writer.WriteFrame(frame);
        }
        var segmentData = ms.ToArray();
        if (segmentData.Length == 0) return;

        lock (_lock)
        {
            foreach (var ns in _clients.ToArray())
            {
                try
                {
                    // 发送 WebSocket 二进制帧
                    WebSocketHelper.SendBinaryFrame(ns, segmentData);
                }
                catch
                {
                    _clients.Remove(ns);
                }
            }
        }
    }

    /// <summary>停止服务</summary>
    public void Stop()
    {
        _running = false;
        _listener?.Stop();
        lock (_lock) _clients.Clear();
    }

    /// <summary>释放资源</summary>
    public void Dispose() => Stop();

    private async Task AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                // 执行 WebSocket 握手
                var stream = client.GetStream();
                if (WebSocketHelper.PerformHandshake(client))
                {
                    lock (_lock) _clients.Add(stream);
                }
                else
                {
                    stream.Dispose();
                }
            }
            catch
            {
                break;
            }
        }
    }
}
