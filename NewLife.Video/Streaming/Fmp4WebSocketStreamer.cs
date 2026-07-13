using NewLife.Data;
using NewLife.Net;
using NewLife.Video.Containers;
using NewLife.Video.FMp4;

namespace NewLife.Video.Streaming;

/// <summary>fMP4 over WebSocket 实时播放服务端。将视频帧封装为 fMP4 分段通过 WebSocket 推送</summary>
/// <remarks>
/// 基于 M2-15 Fmp4SegmentWriter + M6-6 WebSocket 通道，实现服务端→浏览器低延迟播放（1-3秒）。
/// 浏览器端使用 Media Source Extensions (MSE) 播放 fMP4 分段。
/// </remarks>
public class Fmp4WebSocketStreamer : IDisposable
{
    private readonly Int32 _port;
    private readonly String _path;
    private ApiServer _server;
    private readonly List<WebSocket> _clients = [];
    private readonly Object _lock = new();

    /// <summary>是否正在运行</summary>
    public Boolean IsRunning => _server?.Active ?? false;

    /// <summary>客户端连接数</summary>
    public Int32 ClientCount { get { lock (_lock) return _clients.Count; } }

    /// <summary>创建 fMP4 WebSocket 流服务</summary>
    /// <param name="port">监听端口（默认 8080）</param>
    /// <param name="path">WebSocket 路径（默认 /live）</param>
    public Fmp4WebSocketStreamer(Int32 port = 8080, String path = "/live")
    {
        _port = port;
        _path = path;
    }

    /// <summary>启动服务</summary>
    public void Start()
    {
        _server = new ApiServer(_port);
        _server.Start();

        // 注册 WebSocket 处理
        var session = _server.CreateSession();
        session.OnReceived += (s, e) =>
        {
            if (e is WebSocket ws)
            {
                lock (_lock) _clients.Add(ws);
                ws.OnDisposed += (s2, e2) => { lock (_lock) _clients.Remove(ws); };
            }
        };
    }

    /// <summary>推送视频帧（自动封装为 fMP4 分段）</summary>
    /// <param name="frame">视频帧</param>
    public void PushFrame(VPacket frame)
    {
        if (frame?.Data == null) return;

        // 使用内存流构建 fMP4 分段
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
            foreach (var ws in _clients.ToArray())
            {
                try
                {
                    ws.Send(segmentData);
                }
                catch
                {
                    _clients.Remove(ws);
                }
            }
        }
    }

    /// <summary>停止服务</summary>
    public void Stop()
    {
        lock (_lock) _clients.Clear();
        _server?.Stop();
    }

    /// <summary>释放资源</summary>
    public void Dispose() => Stop();
}
