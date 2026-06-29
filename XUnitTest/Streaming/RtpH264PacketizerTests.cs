using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class RtpH264PacketizerTests
{
    private readonly RtpH264Packetizer _packetizer = new(1400);

    [Fact(DisplayName = "RTP封包小NAL单包模式")]
    public void Packetize_SmallNal_SinglePacketMode()
    {
        // 模拟一个小的 NAL 单元（如 SPS）
        var nal = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0xCD };

        var packets = _packetizer.Packetize([nal], 90000, 0);

        Assert.Single(packets);
        Assert.Equal(0, packets[0].SequenceNumber);
        Assert.True(packets[0].Marker);
        Assert.Equal(nal, packets[0].Data);
    }

    [Fact(DisplayName = "RTP封包大NAL分片模式")]
    public void Packetize_LargeNal_FragmentationMode()
    {
        // 模拟一个大 NAL 单元（如 IDR 帧）
        var nal = new Byte[3000];
        nal[0] = 0x65; // IDR NAL header
        for (var i = 1; i < nal.Length; i++)
            nal[i] = (Byte)(i % 256);

        var packets = _packetizer.Packetize([nal], 90000, 100);

        Assert.True(packets.Length > 1, $"Expected >1 packets, got {packets.Length}");

        // 第一个分片：FU indicator + FU header (start)
        Assert.Equal(28, packets[0].Data[0] & 0x1F);  // FU-A type
        Assert.True((packets[0].Data[1] & 0x80) != 0); // Start bit

        // 最后一个分片：End bit
        Assert.True((packets[^1].Data[1] & 0x40) != 0 || packets[^1].Marker);
    }

    [Fact(DisplayName = "RTP解包单NAL还原")]
    public void Depacketize_SingleNal_Restores()
    {
        var nal = new Byte[] { 0x67, 0x42, 0x00, 0x1E };
        var packets = new[]
        {
            new RtpPacket { Data = nal, SequenceNumber = 0, Timestamp = 90000 },
        };

        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nal, nals[0]);
    }

    [Fact(DisplayName = "RTP解包FU-A分片重组")]
    public void Depacketize_FuA_Reassembles()
    {
        var nalPayload = new Byte[100];
        for (var i = 0; i < nalPayload.Length; i++)
            nalPayload[i] = (Byte)(i % 256);

        var originalNalHeader = (Byte)0x65; // IDR

        // 模拟 FU-A 起始分片
        var startFu = new Byte[2 + 50];
        startFu[0] = (Byte)((originalNalHeader & 0xE0) | 28); // FU indicator
        startFu[1] = (Byte)((originalNalHeader & 0x1F) | 0x80); // FU header + Start
        Array.Copy(nalPayload, 0, startFu, 2, 50);

        // FU-A 结束分片
        var endFu = new Byte[2 + 50];
        endFu[0] = (Byte)((originalNalHeader & 0xE0) | 28);
        endFu[1] = (Byte)((originalNalHeader & 0x1F) | 0x40); // FU header + End
        Array.Copy(nalPayload, 50, endFu, 2, 50);

        var packets = new[]
        {
            new RtpPacket { Data = startFu, SequenceNumber = 0 },
            new RtpPacket { Data = endFu, SequenceNumber = 1 },
        };

        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);

        // 验证 NAL header
        Assert.Equal(originalNalHeader, nals[0][0]);

        // 验证载荷数据
        Assert.Equal(1 + nalPayload.Length, nals[0].Length);
        for (var i = 0; i < nalPayload.Length; i++)
            Assert.Equal(nalPayload[i], nals[0][i + 1]);
    }

    [Fact(DisplayName = "RTP封包多NAL聚合")]
    public void Packetize_MultipleNals_CorrectSequence()
    {
        var sps = new Byte[] { 0x67, 0x42, 0x00, 0x1E };
        var pps = new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };
        var idr = new Byte[1500];
        idr[0] = 0x65;

        var packets = _packetizer.Packetize([sps, pps, idr], 90000, 1);

        Assert.True(packets.Length >= 3);
        Assert.Equal(1, packets[0].SequenceNumber);
        Assert.True(packets[^1].Marker); // 最后一个包 Marker=true
    }

    [Fact(DisplayName = "RTP封包解包往返一致")]
    public void Packetize_Depacketize_RoundTrip()
    {
        // 单个小 NAL
        var nal = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0xCD };
        var packets = _packetizer.Packetize([nal], 90000, 0);
        var nals = _packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nal.Length, nals[0].Length);
        Assert.Equal(nal, nals[0]);
    }

    [Fact(DisplayName = "RTP STAP-A聚合包解包")]
    public void Depacketize_StapA_Splits()
    {
        var nal1 = new Byte[] { 0x67, 0x42, 0x00, 0x1E };
        var nal2 = new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };

        // 构建 STAP-A
        var stapData = new Byte[1 + 2 + nal1.Length + 2 + nal2.Length];
        stapData[0] = 24; // STAP-A type
        stapData[1] = (Byte)(nal1.Length >> 8);
        stapData[2] = (Byte)(nal1.Length & 0xFF);
        Array.Copy(nal1, 0, stapData, 3, nal1.Length);
        stapData[3 + nal1.Length] = (Byte)(nal2.Length >> 8);
        stapData[3 + nal1.Length + 1] = (Byte)(nal2.Length & 0xFF);
        Array.Copy(nal2, 0, stapData, 3 + nal1.Length + 2, nal2.Length);

        var packets = new[]
        {
            new RtpPacket { Data = stapData, SequenceNumber = 0 },
        };

        var nals = _packetizer.Depacketize(packets);

        Assert.Equal(2, nals.Length);
        Assert.Equal(nal1, nals[0]);
        Assert.Equal(nal2, nals[1]);
    }

    [Fact(DisplayName = "RTP空NAL输入返回空")]
    public void Packetize_EmptyNals_ReturnsEmpty()
    {
        Assert.Empty(_packetizer.Packetize(null, 0, 0));
        Assert.Empty(_packetizer.Packetize([], 0, 0));
    }

    [Fact(DisplayName = "RTP解包空输入返回空")]
    public void Depacketize_EmptyPackets_ReturnsEmpty()
    {
        Assert.Empty(_packetizer.Depacketize(null));
        Assert.Empty(_packetizer.Depacketize([]));
    }
}
