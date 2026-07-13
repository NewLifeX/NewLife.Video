using System.Net;
using System.Net.Sockets;
using System.Text;
using NewLife.Video.Containers;
using NewLife.Video.FMp4;

namespace NewLife.Video.Streaming;

/// <summary>fMP4 over WebSocket 实时播放服务端</summary>
public class Fmp4WebSocketStreamer : IDisposable
{
    private readonly Int32 _port;
    private TcpListener _listener;
    private readonly List<NetworkStream> _clients = [];
    private readonly Object _lock = new();
    private Boolean _running;

    public Boolean IsRunning => _running;
    public Int32 ClientCount { get { lock (_lock) return _clients.Count; } }

    public Fmp4WebSocketStreamer(Int32 port = 8080)
    {
        _port = port;
    }

    public void Start()
    {
        _running = true;
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

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
                try { ns.Write(segmentData, 0, segmentData.Length); }
                catch { _clients.Remove(ns); }
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();
        lock (_lock) _clients.Clear();
    }

    public void Dispose() => Stop();

    private async Task AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                var ns = client.GetStream();
                lock (_lock) _clients.Add(ns);
            }
            catch { break; }
        }
    }
}
