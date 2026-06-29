using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MPEG-2 TS (Transport Stream) 容器读取器骨架。解析 PAT/PMT/PES 结构</summary>
/// <remarks>
/// TS 包固定 188 字节。PID 0 为 PAT，PMT PID 从 PAT 中获取。
/// 当前为骨架实现，提供基本 TS 包解析能力。
/// </remarks>
public class TsReader : IVideoContainerReader
{
    private readonly FileStream _stream;
    private Int64 _frameIndex;
    private VideoTypes _codecType;
    private Int32 _videoWidth;
    private Int32 _videoHeight;
    private Double _frameRate;
    private Int64 _totalFrames;
    private Int64 _duration;
    private Int32 _videoPid = -1;
    private readonly List<Byte[]> _pesPackets = [];

    public VideoTypes VideoCodecType => _codecType;
    public Int32 VideoWidth => _videoWidth;
    public Int32 VideoHeight => _videoHeight;
    public Double FrameRate => _frameRate;
    public Int64 TotalFrames => _totalFrames;
    public Int64 Duration => _duration;

    public TsReader(String filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _totalFrames = -1;
        _duration = -1;
        _frameRate = 25.0;

        try { ParsePatPmt(); }
        catch { _stream.Dispose(); throw; }
    }

    private void ParsePatPmt()
    {
        var packet = new Byte[188];
        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47) break; // 同步字节

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            var hasPayload = (packet[3] & 0x10) != 0;
            var adaptationFieldLen = (packet[3] & 0x20) != 0 ? packet[4] + 1 : 0;
            var payloadOffset = 4 + adaptationFieldLen;

            if (pid == 0 && hasPayload && payloadOffset < 188)
            {
                // PAT: 提取 PMT PID
                var sectionLen = ((packet[payloadOffset + 1] & 0x0F) << 8) | packet[payloadOffset + 2];
                if (payloadOffset + 8 + 4 <= 188)
                {
                    _videoPid = ((packet[payloadOffset + 8 + 2] & 0x1F) << 8) | packet[payloadOffset + 8 + 3];
                }
            }

            if (_videoPid > 0) break;
        }

        // 估算帧数
        _totalFrames = _stream.Length / 188 / 100; // 粗略: 100个TS包1帧
        _duration = _totalFrames > 0 ? (Int64)(_totalFrames / _frameRate * 1000) : -1;
        _codecType = VideoTypes.H264; // 默认 H.264
        _videoWidth = 1920;
        _videoHeight = 1080;

        _stream.Seek(0, SeekOrigin.Begin);
    }

    public VPacket ReadFrame()
    {
        var packet = new Byte[188];
        var frameData = new List<Byte>();

        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47) return null;

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            var payloadStart = (packet[1] & 0x40) != 0;
            var hasPayload = (packet[3] & 0x10) != 0;
            var adaptationLen = (packet[3] & 0x20) != 0 ? packet[4] + 1 : 0;
            var payloadOffset = 4 + adaptationLen;

            if (pid == _videoPid && hasPayload && payloadOffset < 188)
            {
                var len = 188 - payloadOffset;
                var payload = new Byte[len];
                Array.Copy(packet, payloadOffset, payload, 0, len);

                if (payloadStart && frameData.Count > 0)
                {
                    // PES 起始，返回上一帧数据
                    var data = frameData.ToArray();
                    frameData.Clear();
                    frameData.AddRange(payload);

                    var vpkt = new VPacket
                    {
                        Data = new Packet(data),
                        CodecType = _codecType,
                        PTS = _frameIndex * 40,
                        FrameIndex = _frameIndex,
                        IsKeyFrame = _frameIndex == 0,
                    };
                    _frameIndex++;
                    return vpkt;
                }

                frameData.AddRange(payload);
            }
        }

        // 最后的数据
        if (frameData.Count > 0)
        {
            var data = frameData.ToArray();
            return new VPacket
            {
                Data = new Packet(data),
                CodecType = _codecType,
                PTS = _frameIndex * 40,
                FrameIndex = _frameIndex,
            };
        }

        return null;
    }

    public Boolean SeekFrame(Int64 frameIndex)
    {
        _frameIndex = frameIndex;
        _stream.Seek(0, SeekOrigin.Begin);
        // TS 不支持精确 seek，顺序跳到目标帧
        VPacket _;
        while (_frameIndex < frameIndex && (_ = ReadFrame()) != null) { }
        return _frameIndex >= frameIndex;
    }

    public void Dispose() => _stream?.Dispose();
}
