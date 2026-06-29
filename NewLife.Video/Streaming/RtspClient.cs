using NewLife.Buffers;
using NewLife.Data;
using System.Net.Sockets;
using System.Text;

namespace NewLife.Video.Streaming;

/// <summary>RTSP 客户端。实现 RTSP 信令交互和 RTP 接收</summary>
/// <remarks>
/// RTSP 流程：OPTIONS → DESCRIBE → SETUP → PLAY → (RTP流) → TEARDOWN
/// 基于 TCP Socket，支持 RTSP over TCP 和 RTP over UDP/TCP interleaved。
/// </remarks>
public class RtspClient : IDisposable
{
    private readonly String _url;
    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private Int32 _cSeq;
    private String _sessionId;
    private String _sdp;
    private Int32 _videoRtpPort;
    private Int32 _audioRtpPort;

    /// <summary>RTSP URL</summary>
    public String Url => _url;

    /// <summary>是否已连接</summary>
    public Boolean IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>SDP 描述</summary>
    public String Sdp => _sdp;

    /// <summary>视频 RTP 端口</summary>
    public Int32 VideoRtpPort => _videoRtpPort;

    /// <summary>创建 RTSP 客户端</summary>
    /// <param name="url">RTSP URL (如 rtsp://192.168.1.64:554/stream)</param>
    public RtspClient(String url)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _cSeq = 1;
    }

    /// <summary>连接到 RTSP 服务器</summary>
    public async Task<Boolean> ConnectAsync()
    {
        var uri = ParseUri(_url);
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(uri.Host, uri.Port);
            _networkStream = _tcpClient.GetStream();
            return true;
        }
        catch { return false; }
    }

    /// <summary>发送 OPTIONS 请求</summary>
    public async Task<String> OptionsAsync()
    {
        var request = BuildRequest("OPTIONS");
        var response = await SendRequestAsync(request);
        return response;
    }

    /// <summary>发送 DESCRIBE 请求</summary>
    public async Task<String> DescribeAsync()
    {
        var request = BuildRequest("DESCRIBE", "Accept: application/sdp");
        var response = await SendRequestAsync(request);
        _sdp = ExtractSdp(response);
        return response;
    }

    /// <summary>发送 SETUP 请求（视频轨）</summary>
    public async Task<String> SetupAsync()
    {
        var transport = "RTP/AVP;unicast;client_port=5000-5001";
        var request = BuildRequest("SETUP", $"Transport: {transport}", "video");
        var response = await SendRequestAsync(request);
        ParseSetupResponse(response);
        return response;
    }

    /// <summary>发送 PLAY 请求</summary>
    public async Task<String> PlayAsync()
    {
        var request = BuildRequest("PLAY", $"Session: {_sessionId}");
        var response = await SendRequestAsync(request);
        return response;
    }

    /// <summary>发送 TEARDOWN 请求</summary>
    public async Task<String> TeardownAsync()
    {
        var request = BuildRequest("TEARDOWN", $"Session: {_sessionId}");
        var response = await SendRequestAsync(request);
        return response;
    }

    /// <summary>接收一个视频帧（从 RTP 流中）</summary>
    public async Task<VPacket> ReadFrameAsync()
    {
        if (!IsConnected) return null;

        // 简化: 通过 TCP 读取 RTP interleaved 数据
        var header = new Byte[4];
        var read = await _networkStream.ReadAsync(header, 0, 4);
        if (read < 4) return null;

        // RTP over TCP: $ + channel(1) + length(2)
        if (header[0] == '$')
        {
            var channel = header[1];
            var hr = new SpanReader(header, 2) { IsLittleEndian = false };
            var length = hr.ReadUInt16();
            if (length <= 0 || length > 65535) return null;

            var data = new Byte[length];
            var offset = 0;
            while (offset < length)
            {
                var n = await _networkStream.ReadAsync(data, offset, length - offset);
                if (n <= 0) break;
                offset += n;
            }

            return new VPacket
            {
                Data = new ArrayPacket(data, 0, offset),
                CodecType = VideoTypes.H264,
            };
        }

        return null;
    }

    #region 辅助

    private static (String Host, Int32 Port) ParseUri(String url)
    {
        var u = url.Replace("rtsp://", "");
        var slash = u.IndexOf('/');
        if (slash > 0) u = u.Substring(0, slash);
        var colon = u.LastIndexOf(':');
        if (colon > 0) return (u.Substring(0, colon), Int32.Parse(u.Substring(colon + 1)));
        return (u, 554);
    }

    private String BuildRequest(String method, String extraHeader = null, String track = null)
    {
        var sb = new StringBuilder();
        var trackUrl = track != null ? $"{_url}/{track}" : _url;
        sb.AppendLine($"{method} {trackUrl} RTSP/1.0");
        sb.AppendLine($"CSeq: {_cSeq++}");
        if (_sessionId != null && method != "SETUP")
            sb.AppendLine($"Session: {_sessionId}");
        if (extraHeader != null)
            sb.AppendLine(extraHeader);
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task<String> SendRequestAsync(String request)
    {
        var data = Encoding.ASCII.GetBytes(request);
        await _networkStream.WriteAsync(data, 0, data.Length);

        var buffer = new Byte[8192];
        var ms = new MemoryStream();
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < TimeSpan.FromSeconds(5))
        {
            if (_networkStream.DataAvailable)
            {
                var read = await _networkStream.ReadAsync(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    ms.Write(buffer, 0, read);
                    var response = Encoding.ASCII.GetString(ms.ToArray());
                    if (response.Contains("\r\n\r\n"))
                    {
                        var contentLength = ParseContentLength(response);
                        if (contentLength <= 0 || ms.Length >= contentLength + response.IndexOf("\r\n\r\n") + 4)
                            return response;
                    }
                }
            }
            else
            {
                await Task.Delay(50);
            }
        }

        return Encoding.ASCII.GetString(ms.ToArray());
    }

    private static Int32 ParseContentLength(String response)
    {
        var idx = response.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 0;
        idx += 15;
        var end = response.IndexOf('\r', idx);
        if (end < 0) end = response.IndexOf('\n', idx);
        if (end < 0) return 0;
        return Int32.TryParse(response.Substring(idx, end - idx).Trim(), out var len) ? len : 0;
    }

    private static String ExtractSdp(String describeResponse)
    {
        var idx = describeResponse.IndexOf("\r\n\r\n");
        if (idx < 0) return null;
        return describeResponse.Substring(idx + 4).Trim();
    }

    private void ParseSetupResponse(String response)
    {
        // 提取 Session ID
        var idx = response.IndexOf("Session:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            idx += 8;
            var end = response.IndexOfAny(new[] { '\r', '\n', ';' }, idx);
            if (end < 0) end = response.Length;
            _sessionId = response.Substring(idx, end - idx).Trim();
        }

        // 提取服务器端口
        var serverPortIdx = response.IndexOf("server_port=");
        if (serverPortIdx >= 0)
        {
            serverPortIdx += 12;
            var end = response.IndexOfAny(new[] { '\r', '\n', ';', '-' }, serverPortIdx);
            if (end < 0) end = response.Length;
            _videoRtpPort = Int32.TryParse(response.Substring(serverPortIdx, end - serverPortIdx), out var p) ? p : 0;
        }
    }

    #endregion

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        try { TeardownAsync().Wait(1000); } catch { }
        _networkStream?.Dispose();
        _tcpClient?.Dispose();
    }
}
