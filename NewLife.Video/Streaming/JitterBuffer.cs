namespace NewLife.Video.Streaming;

/// <summary>抖动缓冲器。平滑网络抖动，保证视频帧按序输出</summary>
/// <remarks>
/// 工作原理：缓存收到的帧，按时间戳排序，延迟指定毫秒后按序输出。
/// 适用于 RTP/RTSP 等实时流接收场景。
/// </remarks>
public class JitterBuffer
{
    private readonly Int32 _delayMs;
    private readonly SortedList<Int64, VPacket> _buffer = [];
    private Int64 _nextPts;
    private readonly Object _lock = new();

    /// <summary>缓冲延迟（毫秒）</summary>
    public Int32 DelayMs => _delayMs;

    /// <summary>当前缓冲帧数</summary>
    public Int32 Count
    {
        get { lock (_lock) return _buffer.Count; }
    }

    /// <summary>创建抖动缓冲器</summary>
    /// <param name="delayMs">缓冲延迟（毫秒，建议 100~500）</param>
    public JitterBuffer(Int32 delayMs = 200)
    {
        if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));
        _delayMs = delayMs;
    }

    /// <summary>放入一个视频数据包</summary>
    /// <param name="packet">视频数据包</param>
    public void Enqueue(VPacket packet)
    {
        if (packet == null) return;

        lock (_lock)
        {
            _buffer[packet.PTS] = packet;
        }
    }

    /// <summary>取出下一个可输出的数据包（按延迟时间）</summary>
    /// <param name="currentTimeMs">当前时间（毫秒）</param>
    /// <returns>可输出的数据包，不可输出时返回 null</returns>
    public VPacket Dequeue(Int64 currentTimeMs)
    {
        lock (_lock)
        {
            if (_buffer.Count == 0) return null;

            // 取第一个（最小 PTS）
            var minPts = _buffer.Keys[0];

            // 判断是否到达播放时间
            if (minPts + _delayMs <= currentTimeMs)
            {
                var packet = _buffer[minPts];
                _buffer.RemoveAt(0);
                _nextPts = minPts;
                return packet;
            }
        }

        return null;
    }

    /// <summary>清空缓冲区</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _nextPts = 0;
        }
    }

    /// <summary>获取缓冲区中所有数据包（用于快速清空）</summary>
    /// <returns>数据包列表</returns>
    public List<VPacket> Drain()
    {
        lock (_lock)
        {
            var list = _buffer.Values.ToList();
            _buffer.Clear();
            _nextPts = 0;
            return list;
        }
    }
}
