using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class RtpH265PacketizerTests
{
    private readonly RtpH265Packetizer _packetizer = new(1400);

    [Fact(DisplayName = "RTP H.265封包小NAL单包模式")]
    public void Packetize_SmallNal_SinglePacketMode()
    {
        // H.265 NAL header 2字节
        var nal = new Byte[] { 0x40, 0x01, 0x0A, 0x0B, 0x0C }; // VPS (type=32<<1|0=0x40, layer=0, tid=1)

        var packets = _packetizer.Packetize([nal], 90000, 0);

        Assert.Single(packets);
        Assert.True(packets[0].Marker);
    }

    [Fact(DisplayName = "RTP H.265封包大NAL分片模式")]
    public void Packetize_LargeNal_FragmentationMode()
    {
        var nal = new Byte[3000];
        nal[0] = 0x40; // H.265 NAL header byte 1
        nal[1] = 0x01; // H.265 NAL header byte 2

        var packets = _packetizer.Packetize([nal], 90000, 100);

        Assert.True(packets.Length > 1, $"Expected >1 packets, got {packets.Length}");

        // 第一个分片：FU type=49
        var firstHeader = (UInt16)((packets[0].Data[0] << 8) | packets[0].Data[1]);
        Assert.Equal(49, (firstHeader >> 9) & 0x3F);

        // Start bit (bit 15 of 16-bit FU header)
        var fuHeader = (UInt16)((packets[0].Data[2] << 8) | packets[0].Data[3]);
        Assert.True((fuHeader & 0x8000) != 0);
    }

    [Fact(DisplayName = "RTP H.265解包单NAL还原")]
    public void Depacketize_SingleNal_Restores()
    {
        var nal = new Byte[] { 0x40, 0x01, 0x0A, 0x0B };
        var packets = new[]
        {
            new RtpPacket { Data = nal, SequenceNumber = 0, Timestamp = 90000 },
        };

        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nal, nals[0]);
    }

    [Fact(DisplayName = "RTP H.265解包FU-A分片重组")]
    public void Depacketize_FuA_Reassembles()
    {
        var nalPayload = new Byte[100];
        for (var i = 0; i < nalPayload.Length; i++)
            nalPayload[i] = (Byte)(i % 256);

        // 原始 NAL header (type=32=VPS)
        var nalHeader = (UInt16)((0x40 << 8) | 0x01);
        var nalType = (UInt16)((nalHeader >> 9) & 0x3F); // 32

        // FU indicator: 保留 F+LayerId+TID，替换 type=49
        var fuIndicator = (UInt16)((nalHeader & 0x81FF) | (49 << 9)); // 0x6201

        // FU-A 起始: type=32 + Start bit
        var fuStartVal = (UInt16)(nalType | 0x8000); // 0x8020
        var startFu = new Byte[4 + 50];
        startFu[0] = (Byte)(fuIndicator >> 8);   // 0x62
        startFu[1] = (Byte)(fuIndicator & 0xFF); // 0x01
        startFu[2] = (Byte)(fuStartVal >> 8);   // 0x80
        startFu[3] = (Byte)(fuStartVal & 0xFF); // 0x20
        Array.Copy(nalPayload, 0, startFu, 4, 50);

        // FU-A 结束: type=32 + End bit
        var fuEndVal = (UInt16)(nalType | 0x4000); // 0x4020
        var endFu = new Byte[4 + 50];
        endFu[0] = (Byte)(fuIndicator >> 8);   // 0x62
        endFu[1] = (Byte)(fuIndicator & 0xFF); // 0x01
        endFu[2] = (Byte)(fuEndVal >> 8);   // 0x40
        endFu[3] = (Byte)(fuEndVal & 0xFF); // 0x20
        Array.Copy(nalPayload, 50, endFu, 4, 50);

        var packets = new[]
        {
            new RtpPacket { Data = startFu, SequenceNumber = 0 },
            new RtpPacket { Data = endFu, SequenceNumber = 1 },
        };

        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nalHeader, (UInt16)((nals[0][0] << 8) | nals[0][1]));
    }

    [Fact(DisplayName = "RTP H.265封包解包往返一致")]
    public void Packetize_Depacketize_RoundTrip()
    {
        var nal = new Byte[] { 0x42, 0x01, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E };
        var packets = _packetizer.Packetize([nal], 90000, 0);
        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nal, nals[0]);
    }

    [Fact(DisplayName = "RTP H.265空输入返回空")]
    public void Packetize_Empty_ReturnsEmpty()
    {
        Assert.Empty(_packetizer.Packetize(null, 0, 0));
        Assert.Empty(_packetizer.Packetize([], 0, 0));
    }
}
