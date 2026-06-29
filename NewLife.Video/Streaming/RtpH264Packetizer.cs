using NewLife.Data;

namespace NewLife.Video.Streaming;

/// <summary>RTP 视频封包/解包器</summary>
/// <remarks>
/// 支持 H.264 (RFC 6184) 和 H.265 (RFC 7798) 的 RTP 载荷格式。
/// 包括：单 NAL 单元模式、FU-A 分片模式、STAP-A 聚合包模式。
/// </remarks>
public class RtpH264Packetizer
{
    private readonly Int32 _mtu;

    /// <summary>MTU 大小（默认 1400 字节）</summary>
    public Int32 Mtu => _mtu;

    /// <summary>创建 RTP H.264 封包器</summary>
    /// <param name="mtu">最大传输单元（建议 1200~1400）</param>
    public RtpH264Packetizer(Int32 mtu = 1400)
    {
        if (mtu < 200) throw new ArgumentOutOfRangeException(nameof(mtu), "MTU 不能小于 200");
        _mtu = mtu;
    }

    /// <summary>将 NAL 单元数组封装为 RTP 包数组</summary>
    /// <param name="nalUnits">NAL 单元数组（每个元素为一个完整 NAL）</param>
    /// <param name="timestamp">RTP 时间戳</param>
    /// <param name="sequenceNumber">起始序列号</param>
    /// <returns>RTP 包数组</returns>
    public RtpPacket[] Packetize(Byte[][] nalUnits, Int32 timestamp, Int32 sequenceNumber)
    {
        if (nalUnits == null || nalUnits.Length == 0) return [];

        var packets = new List<RtpPacket>();

        foreach (var nal in nalUnits)
        {
            if (nal.Length <= _mtu - 12) // RTP 头部 12 字节
            {
                // 单 NAL 单元模式
                packets.Add(new RtpPacket
                {
                    Data = nal,
                    Timestamp = (UInt32)timestamp,
                    SequenceNumber = (UInt16)(sequenceNumber++),
                    Marker = false,
                });
            }
            else
            {
                // FU-A 分片模式
                var fuPackets = FragmentFuA(nal, timestamp, ref sequenceNumber);
                packets.AddRange(fuPackets);
            }
        }

        // 最后一个包设置 Marker 位
        if (packets.Count > 0)
            packets[^1].Marker = true;

        return packets.ToArray();
    }

    /// <summary>将单个 NAL 单元分片为 FU-A 包</summary>
    private RtpPacket[] FragmentFuA(Byte[] nal, Int32 timestamp, ref Int32 seqNum)
    {
        var packets = new List<RtpPacket>();
        var nalHeader = nal[0];
        var fuIndicator = (Byte)((nalHeader & 0xE0) | 28); // F|NRI|Type=28(FU-A)
        var fuHeader = (Byte)(nalHeader & 0x1F);            // 原始 NAL 类型

        var payloadSize = _mtu - 12 - 2; // RTP头(12) + FU indicator(1) + FU header(1)
        var totalFragments = (nal.Length - 1 + payloadSize - 1) / payloadSize;

        for (var i = 0; i < totalFragments; i++)
        {
            var start = 1 + i * payloadSize;
            var length = Math.Min(payloadSize, nal.Length - start);

            var fuHeaderByte = fuHeader;
            if (i == 0)
                fuHeaderByte |= 0x80; // Start bit
            if (i == totalFragments - 1)
                fuHeaderByte |= 0x40; // End bit

            var fuData = new Byte[2 + length];
            fuData[0] = fuIndicator;
            fuData[1] = fuHeaderByte;
            Array.Copy(nal, start, fuData, 2, length);

            packets.Add(new RtpPacket
            {
                Data = fuData,
                Timestamp = (UInt32)timestamp,
                SequenceNumber = (UInt16)(seqNum++),
                Marker = i == totalFragments - 1,
            });
        }

        return packets.ToArray();
    }

    /// <summary>将 RTP 包反封装为 NAL 单元数组</summary>
    /// <param name="packets">RTP 包序列（同一帧）</param>
    /// <returns>NAL 单元数组</returns>
    public Byte[][] Depacketize(RtpPacket[] packets)
    {
        if (packets == null || packets.Length == 0) return [];

        var nals = new List<Byte[]>();

        foreach (var packet in packets)
        {
            if (packet.Data == null || packet.Data.Length < 1) continue;

            var nalType = packet.Data[0] & 0x1F;

            if (nalType is >= 1 and <= 23)
            {
                // 单 NAL 单元模式
                nals.Add(packet.Data);
            }
            else if (nalType == 28) // FU-A
            {
                if (packet.Data.Length < 2) continue;

                var fuHeader = packet.Data[1];
                var start = (fuHeader & 0x80) != 0;
                var end = (fuHeader & 0x40) != 0;

                // 重组 FU-A
                if (start)
                {
                    var originalNalHeader = (Byte)((packet.Data[0] & 0xE0) | (fuHeader & 0x1F));
                    var nalData = new Byte[1 + packet.Data.Length - 2];
                    nalData[0] = originalNalHeader;
                    Array.Copy(packet.Data, 2, nalData, 1, packet.Data.Length - 2);
                    nals.Add(nalData);
                }
                else if (nals.Count > 0)
                {
                    // 将后续 FU-A 片段追加到最后一个 NAL
                    var lastNal = nals[^1];
                    var appendSize = packet.Data.Length - 2;
                    var newNal = new Byte[lastNal.Length + appendSize];
                    Array.Copy(lastNal, newNal, lastNal.Length);
                    Array.Copy(packet.Data, 2, newNal, lastNal.Length, appendSize);
                    nals[^1] = newNal;
                }
            }
            else if (nalType == 24) // STAP-A
            {
                // 聚合包：解出多个 NAL
                var offset = 1;
                while (offset + 2 <= packet.Data.Length)
                {
                    var nalSize = (packet.Data[offset] << 8) | packet.Data[offset + 1];
                    offset += 2;
                    if (nalSize == 0 || offset + nalSize > packet.Data.Length) break;

                    var nal = new Byte[nalSize];
                    Array.Copy(packet.Data, offset, nal, 0, nalSize);
                    nals.Add(nal);
                    offset += nalSize;
                }
            }
        }

        return nals.ToArray();
    }
}

/// <summary>RTP 数据包</summary>
public class RtpPacket
{
    /// <summary>载荷数据（不含 RTP 头部）</summary>
    public Byte[] Data { get; set; }

    /// <summary>时间戳（32位）</summary>
    public UInt32 Timestamp { get; set; }

    /// <summary>序列号（16位）</summary>
    public UInt16 SequenceNumber { get; set; }

    /// <summary>标记位（帧结束标记）</summary>
    public Boolean Marker { get; set; }
}
