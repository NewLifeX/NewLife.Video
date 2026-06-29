using NewLife.Data;
using System.Net.Sockets;

namespace NewLife.Video.Streaming;

/// <summary>RTMP 推流客户端。通过 RTMP 协议推送 FLV 封装的视频流到服务器</summary>
/// <remarks>
/// 协议流程：TCP → 握手(C0/C1/C2/S0/S1/S2) → connect → createStream → publish → 推送。
/// 内置 onMetaData/序列头自动发送、Ping 保活、块流复用和协议控制消息处理。
/// </remarks>
public class RtmpClient : IDisposable
{
    private String _url;
    private String _host;
    private Int32 _port;
    private String _appName;
    private String _streamName;
    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private RtmpChunkStream _chunkStream;
    private Int32 _streamId;
    private Int32 _transactionId;
    private Boolean _published;
    private Boolean _sequenceHeaderSent;
    private Boolean _metadataSent;
    private CancellationTokenSource _keepaliveCts;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Byte[] _sps;
    private Byte[] _pps;
    private Byte[] _vps;

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

    public RtmpClient(String url) { _url = url ?? throw new ArgumentNullException(nameof(url)); ParseUrl(); }

    #region 连接

    private void ParseUrl()
    {
        if (!_url.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("RTMP URL 必须以 rtmp:// 开头");

        var uri = _url.Substring(7);
        var slashIdx = uri.IndexOf('/');
        if (slashIdx < 0) throw new ArgumentException("URL 格式错误");

        var hostPart = uri.Substring(0, slashIdx);
        var pathPart = uri.Substring(slashIdx + 1);
        var colonIdx = hostPart.IndexOf(':');
        if (colonIdx > 0) { _host = hostPart.Substring(0, colonIdx); _port = Int32.Parse(hostPart.Substring(colonIdx + 1)); }
        else { _host = hostPart; _port = 1935; }

        var lastSlash = pathPart.LastIndexOf('/');
        if (lastSlash > 0) { _appName = pathPart.Substring(0, lastSlash); _streamName = pathPart.Substring(lastSlash + 1); }
        else { _appName = pathPart; _streamName = ""; }
    }

    public async Task<Boolean> ConnectAsync()
    {
        if (IsConnected) return true;
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            _networkStream = _tcpClient.GetStream();
            await SendHandshakeAsync();
            _chunkStream = new RtmpChunkStream(_networkStream);
            await _chunkStream.SendSetChunkSizeAsync(4096);
            await _chunkStream.SendWindowAckSizeAsync(2500000);
            return true;
        }
        catch { Dispose(); return false; }
    }

    private async Task SendHandshakeAsync()
    {
        _networkStream.WriteByte(0x03);
        var c1 = new Byte[1536];
        var ts = (UInt32)Environment.TickCount;
        c1[0] = (Byte)(ts >> 24); c1[1] = (Byte)(ts >> 16); c1[2] = (Byte)(ts >> 8); c1[3] = (Byte)ts;
        var rng = new Random();
        for (var i = 8; i < c1.Length; i++) c1[i] = (Byte)rng.Next(256);
        await _networkStream.WriteAsync(c1, 0, c1.Length);

        var response = new Byte[3073];
        var read = 0;
        while (read < response.Length)
        {
            var n = await _networkStream.ReadAsync(response, read, response.Length - read);
            if (n == 0) throw new IOException("握手失败");
            read += n;
        }
        var c2 = new Byte[1536];
        Array.Copy(response, 1, c2, 0, 1536);
        await _networkStream.WriteAsync(c2, 0, c2.Length);
    }

    #endregion

    #region 命令

    /// <summary>设置视频元数据</summary>
    public void SetVideoInfo(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _videoWidth = width; _videoHeight = height; _codecType = codecType; _frameRate = frameRate;
    }

    /// <summary>发送 connect 命令</summary>
    public async Task<Boolean> ConnectStreamAsync()
    {
        if (!IsConnected) throw new InvalidOperationException("未连接");

        _transactionId++;
        var cmd = new Amf0Encoder();
        cmd.WriteString("connect"); cmd.WriteNumber(_transactionId);
        cmd.WriteObjectBegin();
        cmd.WriteObjectProperty("app", _appName);
        cmd.WriteObjectProperty("tcUrl", $"rtmp://{_host}:{_port}/{_appName}");
        cmd.WriteObjectProperty("type", "nonprivate");
        cmd.WriteObjectProperty("flashVer", "FMLE/3.0");
        cmd.WriteObjectEnd();

        await _chunkStream.WriteMessageAsync(3, 0, 20, 0, cmd.ToArray());

        for (var i = 0; i < 5; i++)
        {
            var (mt, ms, p) = await _chunkStream.ReadMessageAsync();
            if (p == null) return false;
            if (mt == 20) return new Amf0Decoder(p).TryReadConnectResult(out var ok, out _) && ok;
            await _chunkStream.CheckAckAsync();
        }
        return false;
    }

    /// <summary>发送 createStream 命令</summary>
    public async Task<Int32> CreateStreamAsync()
    {
        if (!IsConnected) throw new InvalidOperationException("未连接");

        _transactionId++;
        var cmd = new Amf0Encoder();
        cmd.WriteString("createStream"); cmd.WriteNumber(_transactionId); cmd.WriteNull();

        await _chunkStream.WriteMessageAsync(3, 0, 20, 0, cmd.ToArray());

        for (var i = 0; i < 5; i++)
        {
            var (mt, ms, p) = await _chunkStream.ReadMessageAsync();
            if (p == null) return -1;
            if (mt == 20)
            {
                var d = new Amf0Decoder(p);
                if (d.TryReadCreateStreamResult(out var sid)) { _streamId = sid; return sid; }
            }
            await _chunkStream.CheckAckAsync();
        }
        return -1;
    }

    /// <summary>发送 publish 命令</summary>
    public async Task<Boolean> PublishAsync()
    {
        if (!IsConnected || _streamId <= 0) throw new InvalidOperationException("未连接或未创建流");

        _transactionId++;
        var cmd = new Amf0Encoder();
        cmd.WriteString("publish"); cmd.WriteNumber(_transactionId);
        cmd.WriteNull(); cmd.WriteString(_streamName); cmd.WriteString("live");

        await _chunkStream.WriteMessageAsync(4, 0, 20, _streamId, cmd.ToArray());
        _published = true;
        StartKeepalive();
        return true;
    }

    #endregion

    #region 推送

    /// <summary>推送视频帧。自动发送 onMetaData 和序列头</summary>
    public async Task PushVideoAsync(VPacket packet)
    {
        if (!_published) throw new InvalidOperationException("尚未 publish");
        if (packet?.Data == null) return;

        if (!_metadataSent)
        {
            await SendOnMetaDataAsync();
            _metadataSent = true;
        }

        if (!_sequenceHeaderSent && packet.IsKeyFrame)
        {
            ExtractParameterSets(packet.Data.ToArray());
            await SendSequenceHeaderAsync();
            _sequenceHeaderSent = true;
        }

        var data = packet.Data.ToArray();
        var flvTag = BuildFlvVideoTag(data, packet.IsKeyFrame);
        var ts = (UInt32)(packet.PTS > 0 ? packet.PTS : 0);
        await _chunkStream.WriteMessageAsync(4, ts, 9, _streamId, flvTag);
    }

    private async Task SendOnMetaDataAsync()
    {
        var meta = new Amf0Encoder();
        meta.WriteString("onMetaData"); meta.WriteEcmaArray(7);
        meta.WriteObjectProperty("duration", (Double)0);
        meta.WriteObjectProperty("width", (Double)_videoWidth);
        meta.WriteObjectProperty("height", (Double)_videoHeight);
        meta.WriteObjectProperty("videocodecid", _codecType == VideoTypes.H265 ? 12.0 : 7.0);
        meta.WriteObjectProperty("framerate", _frameRate);
        meta.WriteObjectProperty("videodatarate", (Double)0);
        meta.WriteObjectProperty("filesize", (Double)0);
        meta.WriteObjectEnd();

        var b = meta.ToArray();
        var tag = new Byte[1 + b.Length];
        tag[0] = 0; Array.Copy(b, 0, tag, 1, b.Length);
        await _chunkStream.WriteMessageAsync(4, 0, 18, _streamId, tag);
    }

    private async Task SendSequenceHeaderAsync()
    {
        var isH265 = _codecType == VideoTypes.H265;
        var sps = _sps ?? (isH265 ? new Byte[] { 0x42, 0x01, 0x01, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9, 0xA0 } : new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0x40, 0x80 });
        var pps = _pps ?? (isH265 ? new Byte[] { 0x44, 0x01, 0xC0, 0x72, 0xB0, 0x46, 0x40 } : new Byte[] { 0x68, 0xCE, 0x3C, 0x80 });

        var avcc = BuildAvcc(sps, pps, isH265);
        var tag = new Byte[5 + avcc.Length];
        tag[0] = (Byte)((1 << 4) | (isH265 ? 12 : 7));
        tag[1] = 0; tag[2] = 0; tag[3] = 0; tag[4] = 0;
        Array.Copy(avcc, 0, tag, 5, avcc.Length);
        await _chunkStream.WriteMessageAsync(4, 0, 9, _streamId, tag);
    }

    private static Byte[] BuildAvcc(Byte[] sps, Byte[] pps, Boolean isH265)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(1);
        if (!isH265)
        {
            ms.WriteByte(sps.Length > 1 ? sps[1] : (Byte)0x42);
            ms.WriteByte(sps.Length > 2 ? sps[2] : (Byte)0x00);
            ms.WriteByte(sps.Length > 3 ? sps[3] : (Byte)0x1E);
            ms.WriteByte(0xFF); ms.WriteByte((Byte)(0xE0 | 1));
            ms.WriteByte((Byte)(sps.Length >> 8)); ms.WriteByte((Byte)(sps.Length & 0xFF));
            ms.Write(sps, 0, sps.Length);
            ms.WriteByte(1);
            ms.WriteByte((Byte)(pps.Length >> 8)); ms.WriteByte((Byte)(pps.Length & 0xFF));
            ms.Write(pps, 0, pps.Length);
        }
        else
        {
            ms.WriteByte((Byte)(((sps.Length > 1 ? (sps[1] >> 6) & 3 : 0) << 6) | ((sps.Length > 1 ? (sps[1] >> 5) & 1 : 0) << 5) | (sps.Length > 1 ? sps[1] & 0x1F : 1)));
            for (var i = 0; i < 10; i++) ms.WriteByte(0x00);
            ms.WriteByte(sps.Length > 3 ? sps[3] : (Byte)0x7A);
            ms.WriteByte(0xF0); ms.WriteByte(0x00); ms.WriteByte(0xFC); ms.WriteByte(0xFC);
            ms.WriteByte(0xF8); ms.WriteByte(0xF8); ms.WriteByte(0x00); ms.WriteByte(0x00);
            ms.WriteByte(0x0F);
            var ac = (Byte)((sps != null ? 1 : 0) + (pps != null ? 1 : 0));
            ms.WriteByte(ac);
            ms.WriteByte(33); ms.WriteByte(0x00); ms.WriteByte(1);
            ms.WriteByte((Byte)(sps.Length >> 8)); ms.WriteByte((Byte)(sps.Length & 0xFF));
            ms.Write(sps, 0, sps.Length);
            ms.WriteByte(34); ms.WriteByte(0x00); ms.WriteByte(1);
            ms.WriteByte((Byte)(pps.Length >> 8)); ms.WriteByte((Byte)(pps.Length & 0xFF));
            ms.Write(pps, 0, pps.Length);
        }
        return ms.ToArray();
    }

    private Byte[] BuildFlvVideoTag(Byte[] data, Boolean isKeyFrame)
    {
        var isH265 = _codecType == VideoTypes.H265;
        var tag = new Byte[5 + data.Length];
        tag[0] = (Byte)(((isKeyFrame ? 1 : 2) << 4) | (isH265 ? 12 : 7));
        tag[1] = 1; tag[2] = 0; tag[3] = 0; tag[4] = 0;
        Array.Copy(data, 0, tag, 5, data.Length);
        return tag;
    }

    #endregion

    #region 参数提取

    private void ExtractParameterSets(Byte[] data)
    {
        if (_codecType == VideoTypes.H265) ExtractHevcSets(data); else ExtractAvcSets(data);
    }

    private void ExtractAvcSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            var scl = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) scl = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1) scl = 4;
            }
            if (scl == 0) { i++; continue; }
            var ns = i + scl; if (ns >= data.Length) break;
            var nt = data[ns] & 0x1F;
            var ne = ns + 1;
            while (ne < data.Length - 3)
            { if (data[ne] == 0 && data[ne + 1] == 0 && (data[ne + 2] == 1 || (data[ne + 2] == 0 && ne + 3 < data.Length && data[ne + 3] == 1))) break; ne++; }
            if (ne > data.Length) ne = data.Length;
            var nd = new Byte[ne - ns]; Array.Copy(data, ns, nd, 0, nd.Length);
            if (nt == 7) _sps = nd; else if (nt == 8) _pps = nd;
            if (_sps != null && _pps != null) break;
            i = ne;
        }
    }

    private void ExtractHevcSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            var scl = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) scl = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1) scl = 4;
            }
            if (scl == 0) { i++; continue; }
            var ns = i + scl; if (ns + 1 >= data.Length) break;
            var nt = (data[ns] >> 1) & 0x3F;
            var ne = ns + 1;
            while (ne < data.Length - 3)
            { if (data[ne] == 0 && data[ne + 1] == 0 && (data[ne + 2] == 1 || (data[ne + 2] == 0 && ne + 3 < data.Length && data[ne + 3] == 1))) break; ne++; }
            if (ne > data.Length) ne = data.Length;
            var nd = new Byte[ne - ns]; Array.Copy(data, ns, nd, 0, nd.Length);
            if (nt == 32) _vps = nd; else if (nt == 33) _sps = nd; else if (nt == 34) _pps = nd;
            if (_vps != null && _sps != null && _pps != null) break;
            i = ne;
        }
    }

    #endregion

    #region 保活

    private void StartKeepalive()
    {
        _keepaliveCts?.Cancel();
        _keepaliveCts = new CancellationTokenSource();
        _ = KeepaliveLoopAsync(_keepaliveCts.Token);
    }

    private async Task KeepaliveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct);
                if (!ct.IsCancellationRequested && IsConnected)
                    await _chunkStream.SendPingAsync(6, 0);
            }
            catch { break; }
        }
    }

    public void Disconnect()
    {
        _keepaliveCts?.Cancel();
        _networkStream?.Close();
        _tcpClient?.Close();
        _published = false; _sequenceHeaderSent = false; _metadataSent = false;
    }

    #endregion

    public void Dispose()
    {
        Disconnect();
        _keepaliveCts?.Dispose();
        _networkStream?.Dispose();
        _tcpClient?.Dispose();
    }
}
