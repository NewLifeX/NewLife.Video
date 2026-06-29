using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Streaming;

/// <summary>RTP H.265 封包/解包器</summary>
/// <remarks>
/// 支持 H.265 (RFC 7798) 的 RTP 载荷格式。
/// 包括：单 NAL 单元模式、FU-A 分片模式（与 H.264 不同，H.265 的 FU header 结构有差异）。
/// H.265 NAL header 为 2 字节（与 H.264 的 1 字节不同）。
/// </remarks>
public class RtpH265Packetizer
{
    private readonly Int32 _mtu;

    /// <summary>MTU 大小（默认 1400 字节）</summary>
    public Int32 Mtu => _mtu;

    /// <summary>创建 RTP H.265 封包器</summary>
    /// <param name="mtu">最大传输单元</param>
    public RtpH265Packetizer(Int32 mtu = 1400)
    {
        if (mtu < 200) throw new ArgumentOutOfRangeException(nameof(mtu), "MTU 不能小于 200");
        _mtu = mtu;
    }

    /// <summary>将 NAL 单元数组封装为 RTP 包数组</summary>
    /// <param name="nalUnits">NAL 单元数组</param>
    /// <param name="timestamp">RTP 时间戳</param>
    /// <param name="sequenceNumber">起始序列号</param>
    /// <returns>RTP 包数组</returns>
    public RtpPacket[] Packetize(Byte[][] nalUnits, Int32 timestamp, Int32 sequenceNumber)
    {
        if (nalUnits == null || nalUnits.Length == 0) return [];

        var packets = new List<RtpPacket>();

        foreach (var nal in nalUnits)
        {
            if (nal.Length <= _mtu - 12)
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
                // FU-A 分片模式 (H.265 AP 类型 49)
                var fuPackets = FragmentFuA(nal, timestamp, ref sequenceNumber);
                packets.AddRange(fuPackets);
            }
        }

        if (packets.Count > 0)
            packets[^1].Marker = true;

        return packets.ToArray();
    }

    /// <summary>将单个 NAL 单元分片为 FU-A 包</summary>
    private RtpPacket[] FragmentFuA(Byte[] nal, Int32 timestamp, ref Int32 seqNum)
    {
        if (nal.Length < 2) return [];

        var packets = new List<RtpPacket>();
        var nalHeader = (UInt16)((nal[0] << 8) | nal[1]);
        var nalType = (nalHeader >> 9) & 0x3F; // H.265 NAL type 在 bit 1-6 (6 bits)

        // FU indicator: 保留原始 NAL header 的 F、LayerId、TID，设置 NAL type=49(FU)
        var fuIndicator = (UInt16)((nalHeader & 0x81FF) | (49 << 9));
        var fuHeaderBase = (UInt16)(nalType); // Start/End 位在封装时设置

        var payloadSize = _mtu - 12 - 4; // RTP头(12) + FU indicator(2) + FU header(2)
        var totalFragments = (nal.Length - 2 + payloadSize - 1) / payloadSize;

        for (var i = 0; i < totalFragments; i++)
        {
            var start = 2 + i * payloadSize;
            var length = Math.Min(payloadSize, nal.Length - start);

            var fuHeader = fuHeaderBase;
            if (i == 0)
                fuHeader |= 0x8000; // Start bit (bit 15)
            if (i == totalFragments - 1)
                fuHeader |= 0x4000; // End bit (bit 14)

            var fuData = new Byte[4 + length];
            var fw = new SpanWriter(fuData) { IsLittleEndian = false };
            fw.Write(fuIndicator);
            fw.Write(fuHeader);
            nal.AsSpan(start, length).CopyTo(fw.GetSpan(length));
            fw.Advance(length);

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
    /// <param name="packets">RTP 包序列</param>
    /// <returns>NAL 单元数组</returns>
    public Byte[][] Depacketize(RtpPacket[] packets)
    {
        if (packets == null || packets.Length == 0) return [];

        var nals = new List<Byte[]>();
        var fragmentedNal = new List<Byte>();

        foreach (var packet in packets)
        {
            if (packet.Data == null || packet.Data.Length < 2) continue;

            var nalHeader = (UInt16)((packet.Data[0] << 8) | packet.Data[1]);
            var nalType = (nalHeader >> 9) & 0x3F;

            if (nalType < 48)
            {
                // 单 NAL 单元模式
                nals.Add(packet.Data);
            }
            else if (nalType == 49) // FU-A
            {
                if (packet.Data.Length < 4) continue;

                var fuHeader = (UInt16)((packet.Data[2] << 8) | packet.Data[3]);
                var start = (fuHeader & 0x8000) != 0;
                var end = (fuHeader & 0x4000) != 0;

                if (start)
                {
                    fragmentedNal.Clear();
                    // 重构 NAL header: 保留 FU indicator 中的 F、LayerId、TID，替换 NAL type
                    var originalNalHeader = (UInt16)((nalHeader & 0x81FF) | ((fuHeader & 0x3F) << 9));
                    fragmentedNal.Add((Byte)(originalNalHeader >> 8));
                    fragmentedNal.Add((Byte)(originalNalHeader & 0xFF));
                }

                // 追加 FU 数据
                for (var i = 4; i < packet.Data.Length; i++)
                    fragmentedNal.Add(packet.Data[i]);

                if (end)
                {
                    nals.Add(fragmentedNal.ToArray());
                    fragmentedNal.Clear();
                }
            }
            else if (nalType == 48) // AP (聚合包)
            {
                var sr = new SpanReader(packet.Data, 2) { IsLittleEndian = false };
                while (sr.Capacity - sr.Position >= 2)
                {
                    var nalSize = sr.ReadUInt16();
                    if (nalSize == 0 || sr.Capacity - sr.Position < nalSize) break;

                    var nalSpan = packet.Data.AsSpan((Int32)sr.Position, nalSize);
                    var nal = nalSpan.ToArray();
                    nals.Add(nal);
                    sr.ReadBytes(nalSize);
                }
            }
        }

        return nals.ToArray();
    }
}
