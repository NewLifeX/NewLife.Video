using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MPEG-2 TS (Transport Stream) 容器读取器。解析 PAT/PMT/PES 结构</summary>
/// <remarks>
/// TS 包固定 188 字节。PID 0 为 PAT，PMT PID 从 PAT 中获取。
/// 从 PMT 中获取视频 PID 和编码类型，从 PES 头中提取 PTS/DTS，
/// 从首个视频帧中解析 SPS 获取宽高。
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
    private Int32 _pmtPid = -1;

    // PES 缓冲区重建
    private readonly List<Byte> _pesBuffer = [];
    private Int64 _lastPts;
    private Int64 _firstPts = -1;
    private Int64 _lastFramePts;
    private Int32 _frameCount;
    private Boolean _spsParsed;

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

        try
        {
            ParsePatPmt();
            PreScanMetadata();
        }
        catch { _stream.Dispose(); throw; }
    }

    #region PAT/PMT 解析

    private void ParsePatPmt()
    {
        var packet = new Byte[188];

        // 第一遍：读 PAT 获取 PMT PID
        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47) break;

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];

            if (pid == 0)
            {
                var (hasPayload, payloadOffset) = GetPayloadInfo(packet);
                if (hasPayload && payloadOffset + 8 <= 188)
                {
                    var pointerField = packet[payloadOffset];
                    var sectionStart = payloadOffset + 1 + pointerField;
                    if (sectionStart + 12 <= 188)
                    {
                        var entryOffset = sectionStart + 8;
                        if (entryOffset + 4 <= 188)
                        {
                            var programNum = (packet[entryOffset] << 8) | packet[entryOffset + 1];
                            if (programNum != 0)
                                _pmtPid = ((packet[entryOffset + 2] & 0x1F) << 8) | packet[entryOffset + 3];
                        }
                    }
                }
            }

            if (_pmtPid > 0) break;
        }

        if (_pmtPid <= 0) return;

        // 第二遍：读 PMT 获取视频 PID 和 stream_type
        _stream.Seek(0, SeekOrigin.Begin);
        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47) break;

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];

            if (pid == _pmtPid)
            {
                var (hasPayload, payloadOffset) = GetPayloadInfo(packet);
                if (hasPayload && payloadOffset + 8 <= 188)
                {
                    var pointerField = packet[payloadOffset];
                    var sectionStart = payloadOffset + 1 + pointerField;
                    if (sectionStart + 12 <= 188)
                    {
                        var programInfoLen = ((packet[sectionStart + 10] & 0x0F) << 8) | packet[sectionStart + 11];
                        var entryOffset = sectionStart + 12 + programInfoLen;

                        while (entryOffset + 5 <= 188)
                        {
                            var streamType = packet[entryOffset];
                            var elementaryPid = ((packet[entryOffset + 1] & 0x1F) << 8) | packet[entryOffset + 2];
                            var esInfoLen = ((packet[entryOffset + 3] & 0x0F) << 8) | packet[entryOffset + 4];

                            if (streamType == 0x1B || streamType == 0x24 || streamType == 0x02)
                            {
                                _videoPid = elementaryPid;
                                _codecType = streamType switch
                                {
                                    0x1B => VideoTypes.H264,
                                    0x24 => VideoTypes.H265,
                                    _ => VideoTypes.Unknown,
                                };
                                break;
                            }

                            entryOffset += 5 + esInfoLen;
                            if (entryOffset >= 188) break;
                        }
                    }
                }
            }

            if (_videoPid > 0) break;
        }

        _stream.Seek(0, SeekOrigin.Begin);
    }

    private static (Boolean hasPayload, Int32 offset) GetPayloadInfo(Byte[] packet)
    {
        var adaptationFieldControl = (packet[3] >> 4) & 0x03;
        var hasPayload = (adaptationFieldControl & 0x01) != 0;
        var hasAdaptation = (adaptationFieldControl & 0x02) != 0;
        var offset = 4;
        if (hasAdaptation)
            offset += 1 + packet[4];
        return (hasPayload, Math.Min(offset, 188));
    }

    #endregion

    #region 元数据预扫描

    private void PreScanMetadata()
    {
        if (_videoPid <= 0) { _totalFrames = 0; return; }

        var packet = new Byte[188];

        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47) break;

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            if (pid != _videoPid) continue;

            var payloadStart = (packet[1] & 0x40) != 0;
            var (hasPayload, payloadOffset) = GetPayloadInfo(packet);
            if (!hasPayload || payloadOffset >= 188) continue;

            var payloadLen = 188 - payloadOffset;

            if (payloadStart)
            {
                if (payloadLen >= 9)
                {
                    var pts = ParsePesTimestamp(packet, payloadOffset, payloadLen);
                    if (pts >= 0)
                    {
                        _lastPts = pts;
                        if (_firstPts < 0) _firstPts = pts;
                    }
                }

                if (!_spsParsed && payloadLen > 9)
                    TryExtractSps(packet, payloadOffset, payloadLen);

                _frameCount++;
                _lastFramePts = _lastPts;
            }
        }

        _totalFrames = _frameCount;

        if (_frameCount > 1 && _firstPts >= 0 && _lastFramePts > _firstPts)
        {
            var duration90k = _lastFramePts - _firstPts;
            _duration = duration90k * 1000 / 90000;
            _frameRate = (Double)(_frameCount - 1) / (_duration / 1000.0);
        }
        else if (_totalFrames > 0 && _frameRate > 0)
        {
            _duration = (Int64)(_totalFrames / _frameRate * 1000);
        }

        _stream.Seek(0, SeekOrigin.Begin);
    }

    private static Int64 ParsePesTimestamp(Byte[] packet, Int32 offset, Int32 len)
    {
        if (len < 9) return -1;
        if (packet[offset] != 0 || packet[offset + 1] != 0 || packet[offset + 2] != 1)
            return -1;

        var streamId = packet[offset + 3];
        if ((streamId & 0xF0) != 0xE0 && (streamId & 0xF0) != 0xC0) return -1;

        var hasPts = (packet[offset + 7] & 0x80) != 0;
        if (!hasPts) return -1;

        var ptsOffset = offset + 9;
        if (ptsOffset + 5 > offset + len) return -1;

        var pts = ((Int64)(packet[ptsOffset] & 0x0E) << 29)
                | ((Int64)packet[ptsOffset + 1] << 22)
                | ((Int64)(packet[ptsOffset + 2] & 0xFE) << 14)
                | ((Int64)packet[ptsOffset + 3] << 7)
                | ((Int64)(packet[ptsOffset + 4] & 0xFE) >> 1);

        return pts;
    }

    private void TryExtractSps(Byte[] packet, Int32 offset, Int32 len)
    {
        _spsParsed = true;

        if (len < 9) return;
        var pesHeaderDataLen = packet[offset + 8];
        var esOffset = offset + 9 + pesHeaderDataLen;
        if (esOffset >= offset + len) return;

        var esLen = offset + len - esOffset;
        if (esLen < 10) return;

        for (var i = esOffset; i < offset + len - 4; i++)
        {
            if (packet[i] != 0 || packet[i + 1] != 0) continue;

            Int32 nalStart;
            if (packet[i + 2] == 1)
                nalStart = i + 3;
            else if (packet[i + 2] == 0 && i + 3 < offset + len && packet[i + 3] == 1)
                nalStart = i + 4;
            else
                continue;

            if (nalStart >= offset + len) break;

            var nalType = _codecType == VideoTypes.H265
                ? (packet[nalStart] >> 1) & 0x3F
                : packet[nalStart] & 0x1F;

            if ((_codecType == VideoTypes.H264 && nalType == 7) ||
                (_codecType == VideoTypes.H265 && nalType == 33))
            {
                var spsEnd = nalStart + 1;
                for (var j = nalStart + 1; j < offset + len - 3; j++)
                {
                    if (packet[j] == 0 && packet[j + 1] == 0 &&
                        (packet[j + 2] == 1 || (packet[j + 2] == 0 && j + 3 < offset + len && packet[j + 3] == 1)))
                    { spsEnd = j; break; }
                }
                if (spsEnd <= offset + len) spsEnd = offset + len;

                var spsLen = spsEnd - nalStart;
                if (spsLen >= 4)
                {
                    var spsData = new Byte[spsLen];
                    Array.Copy(packet, nalStart, spsData, 0, spsLen);
                    ParseSps(spsData);
                }
                return;
            }
        }
    }

    private void ParseSps(Byte[] sps)
    {
        if (sps.Length < 4) return;
        try
        {
            var isH265 = _codecType == VideoTypes.H265;
            var bitReader = new BitReader(sps, isH265 ? 2 * 8 : 1 * 8);

            if (!isH265)
            {
                var profileIdc = sps.Length > 1 ? sps[1] : (Byte)0;
                bitReader.ReadUeGolomb();
                if (profileIdc >= 100)
                {
                    var cfi = bitReader.ReadUeGolomb();
                    if (cfi == 3) bitReader.SkipBits(1);
                    bitReader.ReadUeGolomb(); bitReader.ReadUeGolomb();
                    bitReader.SkipBits(1);
                    if (bitReader.ReadBit() != 0)
                    {
                        var mc = cfi != 3 ? 8 : 12;
                        for (var m = 0; m < mc; m++)
                            if (bitReader.ReadBit() != 0)
                            {
                                var ssl = m < 6 ? 16 : 64; var ls = 8; var ns = 8;
                                for (var j = 0; j < ssl; j++)
                                { if (ns != 0) { var ds = bitReader.ReadSeGolomb(); ns = (ls + ds + 256) % 256; } ls = ns == 0 ? ls : ns; }
                            }
                    }
                }
                bitReader.ReadUeGolomb();
                var pct = bitReader.ReadUeGolomb();
                if (pct == 0) bitReader.ReadUeGolomb();
                else if (pct == 1) { bitReader.SkipBits(1); bitReader.ReadSeGolomb(); bitReader.ReadSeGolomb(); var nr = bitReader.ReadUeGolomb(); for (var r = 0; r < nr; r++) bitReader.ReadSeGolomb(); }
                bitReader.ReadUeGolomb();
                bitReader.SkipBits(1);
                _videoWidth = (bitReader.ReadUeGolomb() + 1) * 16;
                _videoHeight = (bitReader.ReadUeGolomb() + 1) * 16;
                if (bitReader.ReadBit() == 0) _videoHeight *= 2;
            }
            else
            {
                bitReader.SkipBits(4); bitReader.ReadBits(3);
                bitReader.SkipBits(1 + 80);
                bitReader.ReadUeGolomb();
                var cfi = bitReader.ReadUeGolomb();
                if (cfi == 3) bitReader.SkipBits(1);
                _videoWidth = bitReader.ReadUeGolomb();
                _videoHeight = bitReader.ReadUeGolomb();
            }
        }
        catch { }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<Byte> _data;
        private Int32 _bitPosition;
        public BitReader(ReadOnlySpan<Byte> data, Int32 startBit) { _data = data; _bitPosition = startBit; }
        public Int32 ReadBit() { if (_bitPosition / 8 >= _data.Length) return 0; var bi = _bitPosition / 8; var bb = 7 - (_bitPosition % 8); _bitPosition++; return (_data[bi] >> bb) & 1; }
        public Int32 ReadBits(Int32 count) { var v = 0; for (var i = 0; i < count; i++) v = (v << 1) | ReadBit(); return v; }
        public void SkipBits(Int32 count) => _bitPosition += count;
        public Int32 ReadUeGolomb() { var lz = 0; while (ReadBit() == 0 && lz < 32) lz++; if (lz >= 32) return 0; var v = 0; for (var i = 0; i < lz; i++) v = (v << 1) | ReadBit(); return (1 << lz) - 1 + v; }
        public Int32 ReadSeGolomb() { var ue = ReadUeGolomb(); return (ue & 1) == 0 ? -(ue >> 1) : (ue + 1) >> 1; }
    }

    #endregion

    #region 帧读取

    public VPacket ReadFrame()
    {
        var packet = new Byte[188];
        var frameData = new List<Byte>();
        var pesStarted = false;

        while (_stream.Read(packet, 0, 188) == 188)
        {
            if (packet[0] != 0x47)
            {
                if (frameData.Count > 0) break;
                return null;
            }

            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            if (pid != _videoPid) continue;

            var payloadStart = (packet[1] & 0x40) != 0;
            var (hasPayload, payloadOffset) = GetPayloadInfo(packet);
            if (!hasPayload || payloadOffset >= 188) continue;

            var payloadLen = 188 - payloadOffset;

            if (payloadStart)
            {
                if (pesStarted && frameData.Count > 0)
                {
                    _stream.Seek(-188, SeekOrigin.Current);
                    break;
                }

                pesStarted = true;

                if (payloadLen >= 9)
                {
                    var pts = ParsePesTimestamp(packet, payloadOffset, payloadLen);
                    if (pts >= 0) _lastPts = pts;
                }

                var pesHeaderDataLen = payloadLen > 8 ? packet[payloadOffset + 8] : 0;
                var esOffset = payloadOffset + 9 + pesHeaderDataLen;
                if (esOffset < 188)
                {
                    var esLen = Math.Min(188 - esOffset, payloadLen - (esOffset - payloadOffset));
                    if (esLen > 0)
                    {
                        var esData = new Byte[esLen];
                        Array.Copy(packet, esOffset, esData, 0, esLen);
                        frameData.AddRange(esData);
                    }
                }
            }
            else if (pesStarted)
            {
                var data = new Byte[payloadLen];
                Array.Copy(packet, payloadOffset, data, 0, payloadLen);
                frameData.AddRange(data);
            }
        }

        if (frameData.Count > 0)
        {
            var data = frameData.ToArray();
            return new VPacket
            {
                Data = new ArrayPacket(data),
                CodecType = _codecType,
                PTS = _lastPts > 0 ? _lastPts * 1000 / 90000 : _frameIndex * 40,
                FrameIndex = _frameIndex++,
                IsKeyFrame = _frameIndex == 0,
            };
        }

        return null;
    }

    public Boolean SeekFrame(Int64 frameIndex)
    {
        _frameIndex = frameIndex;
        _stream.Seek(0, SeekOrigin.Begin);
        while (_frameIndex < frameIndex && ReadFrame() != null) { }
        return _frameIndex >= frameIndex;
    }

    public void Dispose() { if (_disposed) return; _disposed = true; _stream?.Dispose(); }
    private Boolean _disposed;

    #endregion
}
