using System.Net.Sockets;

namespace NewLife.Video.Streaming;

/// <summary>RTMP 块流管理器。管理单个逻辑流的块状态和协议控制消息</summary>
/// <remarks>
/// RTMP 将消息拆分为块（chunk）通过 TCP 传输。每个块流（chunk stream）有独立的 csid，
/// 块头长度根据 fmt 可变（0=12字节, 1=8字节, 2=4字节, 3=1字节）。
/// 协议控制消息（Set Chunk Size/Acknowledgement/Window ACK 等）在此层处理。
/// </remarks>
public class RtmpChunkStream
{
    private readonly NetworkStream _stream;
    private Int32 _inChunkSize = 128;
    private Int32 _outChunkSize = 128;
    private UInt32 _windowSize = 2500000;
    private UInt32 _bytesReceived;
    private UInt32 _bytesSent;
    private UInt32 _lastAckSent;

    private readonly Byte[] _readBuf = new Byte[1];

    // 每个 csid 的读取状态
    private readonly Dictionary<Int32, ChunkReadState> _readStates = [];

    /// <summary>收到的字节数</summary>
    public UInt32 BytesReceived => _bytesReceived;

    /// <summary>发送的字节数</summary>
    public UInt32 BytesSent => _bytesSent;

    public RtmpChunkStream(NetworkStream stream) => _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    #region 写入

    /// <summary>发送一条 RTMP 消息（自动分块）</summary>
    public async Task WriteMessageAsync(Int32 csid, UInt32 timestamp, Byte msgType, Int32 msgStreamId, Byte[] payload)
    {
        var payloadOffset = 0;
        var firstChunk = true;

        while (payloadOffset < payload.Length)
        {
            var chunkSize = Math.Min(_outChunkSize, payload.Length - payloadOffset);
            var fmt = (Byte)(firstChunk ? 0 : 3);

            // 构建基本头
            WriteBasicHeader(csid, fmt);

            // 构建消息头
            if (firstChunk)
            {
                WriteMessageHeader(fmt, timestamp, payload.Length, msgType, msgStreamId);
                firstChunk = false;
            }

            // 写入块数据
            await _stream.WriteAsync(payload, payloadOffset, chunkSize);
            _bytesSent += (UInt32)chunkSize;
            payloadOffset += chunkSize;
        }
    }

    private void WriteBasicHeader(Int32 csid, Byte fmt)
    {
        if (csid < 64)
        {
            _stream.WriteByte((Byte)((fmt << 6) | csid));
        }
        else if (csid < 320)
        {
            _stream.WriteByte((Byte)((fmt << 6)));
            _stream.WriteByte((Byte)(csid - 64));
        }
        else
        {
            _stream.WriteByte((Byte)((fmt << 6) | 1));
            var b = csid - 64;
            _stream.WriteByte((Byte)(b & 0xFF));
            _stream.WriteByte((Byte)((b >> 8) & 0xFF));
        }
    }

    private void WriteMessageHeader(Byte fmt, UInt32 timestamp, Int32 msgLen, Byte msgType, Int32 msgStreamId)
    {
        var th = timestamp >= 0xFFFFFF ? (UInt32)0xFFFFFF : timestamp;

        // fmt 0: timestamp(3) + msgLen(3) + msgType(1) + msgStreamId(4, LE)
        var buf = new Byte[11];
        buf[0] = (Byte)((th >> 16) & 0xFF);
        buf[1] = (Byte)((th >> 8) & 0xFF);
        buf[2] = (Byte)(th & 0xFF);
        buf[3] = (Byte)((msgLen >> 16) & 0xFF);
        buf[4] = (Byte)((msgLen >> 8) & 0xFF);
        buf[5] = (Byte)(msgLen & 0xFF);
        buf[6] = msgType;
        buf[7] = (Byte)(msgStreamId & 0xFF);
        buf[8] = (Byte)((msgStreamId >> 8) & 0xFF);
        buf[9] = (Byte)((msgStreamId >> 16) & 0xFF);
        buf[10] = (Byte)((msgStreamId >> 24) & 0xFF);

        _stream.Write(buf, 0, 11);

        // 扩展时间戳
        if (timestamp >= 0xFFFFFF)
        {
            buf[0] = (Byte)((timestamp >> 24) & 0xFF);
            buf[1] = (Byte)((timestamp >> 16) & 0xFF);
            buf[2] = (Byte)((timestamp >> 8) & 0xFF);
            buf[3] = (Byte)(timestamp & 0xFF);
            _stream.Write(buf, 0, 4);
        }
    }

    /// <summary>发送协议控制消息：Set Chunk Size</summary>
    public async Task SendSetChunkSizeAsync(Int32 chunkSize)
    {
        var payload = new Byte[4];
        payload[0] = (Byte)((chunkSize >> 24) & 0xFF);
        payload[1] = (Byte)((chunkSize >> 16) & 0xFF);
        payload[2] = (Byte)((chunkSize >> 8) & 0xFF);
        payload[3] = (Byte)(chunkSize & 0xFF);
        _outChunkSize = chunkSize;
        await WriteMessageAsync(2, 0, 1, 0, payload);
    }

    /// <summary>发送 Acknowledgement（确认已收到字节数）</summary>
    public async Task SendAckAsync(UInt32 seqNum)
    {
        var payload = new Byte[4];
        payload[0] = (Byte)((seqNum >> 24) & 0xFF);
        payload[1] = (Byte)((seqNum >> 16) & 0xFF);
        payload[2] = (Byte)((seqNum >> 8) & 0xFF);
        payload[3] = (Byte)(seqNum & 0xFF);
        await WriteMessageAsync(2, 0, 3, 0, payload);
    }

    /// <summary>发送 Window Acknowledgement Size</summary>
    public async Task SendWindowAckSizeAsync(UInt32 size)
    {
        var payload = new Byte[4];
        payload[0] = (Byte)((size >> 24) & 0xFF);
        payload[1] = (Byte)((size >> 16) & 0xFF);
        payload[2] = (Byte)((size >> 8) & 0xFF);
        payload[3] = (Byte)(size & 0xFF);
        await WriteMessageAsync(2, 0, 5, 0, payload);
    }

    /// <summary>发送 RTMP Ping（User Control Message）</summary>
    public async Task SendPingAsync(UInt16 eventType, UInt32 eventData)
    {
        var payload = new Byte[6];
        payload[0] = (Byte)((eventType >> 8) & 0xFF);
        payload[1] = (Byte)(eventType & 0xFF);
        payload[2] = (Byte)((eventData >> 24) & 0xFF);
        payload[3] = (Byte)((eventData >> 16) & 0xFF);
        payload[4] = (Byte)((eventData >> 8) & 0xFF);
        payload[5] = (Byte)(eventData & 0xFF);
        await WriteMessageAsync(2, 0, 4, 0, payload);
    }

    #endregion

    #region 读取

    /// <summary>读取一条完整的 RTMP 消息</summary>
    public async Task<(Byte MsgType, Int32 MsgStreamId, Byte[] Payload)> ReadMessageAsync()
    {
        while (true)
        {
            // 读取基本头首字节
            var first = await ReadByteAsync();
            if (first < 0) return (0, 0, null);

            var fmt = (Byte)((first >> 6) & 0x03);
            var csid = ReadCsid((Byte)first);

            // 获取或创建读取状态
            if (!_readStates.TryGetValue(csid, out var state))
                _readStates[csid] = state = new ChunkReadState();

            // 读取消息头
            var headerLen = fmt switch { 0 => 11, 1 => 7, 2 => 3, _ => 0 };
            Byte[] header = null;
            if (headerLen > 0)
            {
                header = new Byte[headerLen];
                await ReadExactAsync(header, 0, headerLen);
                _bytesReceived += (UInt32)headerLen;
            }

            // 解析时间戳
            UInt32 timestamp;
            if (fmt == 0)
            {
                timestamp = ((UInt32)header[0] << 16) | ((UInt32)header[1] << 8) | header[2];
                state.MsgLen = (header[3] << 16) | (header[4] << 8) | header[5];
                state.MsgType = header[6];
                state.MsgStreamId = header[7] | (header[8] << 8) | (header[9] << 16) | (header[10] << 24);
                state.Timestamp = timestamp;
            }
            else if (fmt == 1)
            {
                var delta = ((UInt32)header[0] << 16) | ((UInt32)header[1] << 8) | header[2];
                state.MsgLen = (header[3] << 16) | (header[4] << 8) | header[5];
                state.MsgType = header[6];
                state.Timestamp += delta;
            }
            else if (fmt == 2)
            {
                var delta = ((UInt32)header[0] << 16) | ((UInt32)header[1] << 8) | header[2];
                state.Timestamp += delta;
            }
            timestamp = state.Timestamp;

            // 扩展时间戳
            if (timestamp == 0xFFFFFF)
            {
                var ext = new Byte[4];
                await ReadExactAsync(ext, 0, 4);
                _bytesReceived += 4;
                timestamp = ((UInt32)ext[0] << 24) | ((UInt32)ext[1] << 16) | ((UInt32)ext[2] << 8) | ext[3];
                state.Timestamp = timestamp;
            }

            // 读取块数据
            var remaining = state.MsgLen - state.BytesRead;
            var chunkDataLen = Math.Min(_inChunkSize, remaining);
            var chunkData = new Byte[chunkDataLen];
            await ReadExactAsync(chunkData, 0, chunkDataLen);
            _bytesReceived += (UInt32)chunkDataLen;

            // 追加到消息缓冲区
            if (state.Buffer == null || state.Buffer.Length < state.BytesRead + chunkDataLen)
            {
                var newBuf = new Byte[state.BytesRead + chunkDataLen];
                if (state.Buffer != null) Array.Copy(state.Buffer, 0, newBuf, 0, state.BytesRead);
                state.Buffer = newBuf;
            }
            Array.Copy(chunkData, 0, state.Buffer, state.BytesRead, chunkDataLen);
            state.BytesRead += chunkDataLen;

            // 协议控制消息（csid 2）内联处理
            if (csid == 2 && state.BytesRead >= state.MsgLen)
            {
                var payload = new Byte[state.MsgLen];
                Array.Copy(state.Buffer, 0, payload, 0, state.MsgLen);
                HandleProtocolMessage(state.MsgType, payload);
                _readStates.Remove(csid);
                continue;
            }

            // 消息完整
            if (state.BytesRead >= state.MsgLen)
            {
                var payload = new Byte[state.MsgLen];
                Array.Copy(state.Buffer, 0, payload, 0, state.MsgLen);
                var msgType = state.MsgType;
                var msgSid = state.MsgStreamId;
                _readStates.Remove(csid);
                return (msgType, msgSid, payload);
            }
        }
    }

    private void HandleProtocolMessage(Byte msgType, Byte[] payload)
    {
        switch (msgType)
        {
            case 1: // Set Chunk Size
                if (payload.Length >= 4)
                    _inChunkSize = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
                break;
            case 3: // Acknowledgement
                break;
            case 5: // Window Acknowledgement Size
                if (payload.Length >= 4)
                    _windowSize = ((UInt32)payload[0] << 24) | ((UInt32)payload[1] << 16) | ((UInt32)payload[2] << 8) | payload[3];
                break;
            case 6: // Set Peer Bandwidth
                break;
        }
    }

    /// <summary>检查是否需要发送 ACK</summary>
    public async Task CheckAckAsync()
    {
        if (_windowSize > 0 && _bytesReceived - _lastAckSent >= _windowSize)
        {
            await SendAckAsync(_bytesReceived);
            _lastAckSent = _bytesReceived;
        }
    }

    private Int32 ReadCsid(Byte first)
    {
        var id = first & 0x3F;
        if (id == 0)
        {
            var b = (Byte)_stream.ReadByte();
            return b + 64;
        }
        if (id == 1)
        {
            var b1 = (Byte)_stream.ReadByte();
            var b2 = (Byte)_stream.ReadByte();
            return ((b2 << 8) | b1) + 64;
        }
        return id;
    }

    private async Task<Int32> ReadByteAsync()
    {
        var n = await _stream.ReadAsync(_readBuf, 0, 1);
        if (n == 0) return -1;
        _bytesReceived++;
        return _readBuf[0];
    }

    private async Task ReadExactAsync(Byte[] buf, Int32 offset, Int32 count)
    {
        var total = 0;
        while (total < count)
        {
            var n = await _stream.ReadAsync(buf, offset + total, count - total);
            if (n == 0) throw new IOException(L.T("The RTMP connection was closed."));
            total += n;
        }
    }

    #endregion

    private class ChunkReadState
    {
        public UInt32 Timestamp;
        public Int32 MsgLen;
        public Byte MsgType;
        public Int32 MsgStreamId;
        public Int32 BytesRead;
        public Byte[] Buffer;
    }
}
