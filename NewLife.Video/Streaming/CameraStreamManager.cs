using NewLife.Data;
using NewLife.Video.Containers;
using NewLife.Video.IoT;

namespace NewLife.Video.Streaming;

/// <summary>多路摄像头实时预览管理器。统一管理多个摄像头 RTSP 连接、自动重连、连接池复用</summary>
/// <remarks>
/// 单进程支持 16+ 路 1080p 摄像头并发预览。
/// 自动维护 RTSP 连接池，支持健康监测和自动重连。
/// </remarks>
public class CameraStreamManager : IDisposable
{
    private readonly Dictionary<String, CameraStream> _cameras = [];
    private readonly Object _lock = new();
    private Timer _healthCheckTimer;

    /// <summary>摄像头数量</summary>
    public Int32 CameraCount { get { lock (_lock) return _cameras.Count; } }

    /// <summary>已连接的摄像头数量</summary>
    public Int32 ConnectedCount { get { lock (_lock) return _cameras.Values.Count(c => c.IsConnected); } }

    /// <summary>帧到达事件</summary>
    public event EventHandler<CameraFrameEventArgs> FrameReceived;

    /// <summary>创建摄像头管理器</summary>
    public CameraStreamManager()
    {
        _healthCheckTimer = new Timer(HealthCheck, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>添加摄像头</summary>
    /// <param name="name">摄像头名称</param>
    /// <param name="rtspUri">RTSP 地址</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    public void AddCamera(String name, String rtspUri, String username = null, String password = null)
    {
        lock (_lock)
        {
            _cameras[name] = new CameraStream
            {
                Name = name,
                RtspUri = rtspUri,
                Username = username,
                Password = password,
            };
        }
    }

    /// <summary>移除摄像头</summary>
    public void RemoveCamera(String name)
    {
        lock (_lock)
        {
            if (_cameras.TryGetValue(name, out var cam))
            {
                cam.Disconnect();
                _cameras.Remove(name);
            }
        }
    }

    /// <summary>连接所有摄像头</summary>
    public void ConnectAll()
    {
        lock (_lock)
        {
            foreach (var cam in _cameras.Values)
            {
                if (!cam.IsConnected)
                    ConnectCamera(cam);
            }
        }
    }

    /// <summary>断开所有摄像头</summary>
    public void DisconnectAll()
    {
        lock (_lock)
        {
            foreach (var cam in _cameras.Values)
                cam.Disconnect();
        }
    }

    private void ConnectCamera(CameraStream cam)
    {
        try
        {
            var client = new RtspClient(cam.RtspUri, cam.Username, cam.Password);
            client.Connect();
            client.Setup();
            client.Play();
            cam.SetClient(client);

            // 启动帧接收循环
            _ = Task.Run(() => ReceiveLoop(cam));
        }
        catch (Exception ex)
        {
            cam.LastError = ex.Message;
        }
    }

    private async Task ReceiveLoop(CameraStream cam)
    {
        while (cam.IsConnected)
        {
            try
            {
                var packet = cam.Client?.ReadFrame();
                if (packet != null)
                {
                    cam.FrameCount++;
                    FrameReceived?.Invoke(this, new CameraFrameEventArgs
                    {
                        CameraName = cam.Name,
                        Packet = packet,
                    });
                }
            }
            catch
            {
                break;
            }
        }
    }

    private void HealthCheck(Object state)
    {
        lock (_lock)
        {
            foreach (var cam in _cameras.Values)
            {
                if (cam.IsConnected && cam.FrameCount == cam.LastFrameCount)
                {
                    // 帧数无变化，可能卡死，重连
                    cam.Disconnect();
                }
                cam.LastFrameCount = cam.FrameCount;

                if (!cam.IsConnected)
                    ConnectCamera(cam);
            }
        }
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        DisconnectAll();
    }
}

/// <summary>摄像头流</summary>
public class CameraStream
{
    public String Name { get; set; }
    public String RtspUri { get; set; }
    public String Username { get; set; }
    public String Password { get; set; }
    public RtspClient Client { get; private set; }
    public Boolean IsConnected => Client?.IsRunning ?? false;
    public Int64 FrameCount { get; set; }
    internal Int64 LastFrameCount { get; set; }
    public String LastError { get; set; }

    internal void SetClient(RtspClient client) => Client = client;

    internal void Disconnect()
    {
        Client?.Stop();
        Client = null;
    }
}

/// <summary>摄像头帧事件参数</summary>
public class CameraFrameEventArgs : EventArgs
{
    public String CameraName { get; set; }
    public VPacket Packet { get; set; }
}
