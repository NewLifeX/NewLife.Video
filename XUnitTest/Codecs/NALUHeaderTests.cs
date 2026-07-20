using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class NALUHeaderTests
{
    [Fact(DisplayName = "NALUHeader 从字节构造")]
    public void Constructor_FromByte()
    {
        // 0x67 = forbidden=0, nal_ref=3, type=7(SPS)
        var header = new NALUHeader(0x67);
        Assert.Equal(0, header.ForbiddenZeroBit);
        Assert.Equal(3, header.NalRefIdc);
        Assert.Equal(NalUnitType.SPS, header.NalUnitType);
        Assert.False(header.KeyFrame);
    }

    [Fact(DisplayName = "NALUHeader IDR 关键帧检测")]
    public void Constructor_IDR_KeyFrame()
    {
        // 0x25 = forbidden=0, nal_ref=0, type=5(IDR)
        var header = new NALUHeader(0x25);
        Assert.Equal(NalUnitType.IDR, header.NalUnitType);
        Assert.True(header.KeyFrame);
    }

    [Fact(DisplayName = "NALUHeader 从 Span 构造")]
    public void Constructor_FromSpan()
    {
        var data = new Byte[] { 0x41 }; // type=1(SLICE), nal_ref=2
        var header = new NALUHeader(data.AsSpan());
        Assert.Equal(NalUnitType.SLICE, header.NalUnitType);
        Assert.Equal(2, header.NalRefIdc);
    }

    [Fact(DisplayName = "NalUnitType 枚举值")]
    public void NalUnitType_Values()
    {
        Assert.Equal(0, (Int32)NalUnitType.None);
        Assert.Equal(1, (Int32)NalUnitType.SLICE);
        Assert.Equal(5, (Int32)NalUnitType.IDR);
        Assert.Equal(6, (Int32)NalUnitType.SEI);
        Assert.Equal(7, (Int32)NalUnitType.SPS);
        Assert.Equal(8, (Int32)NalUnitType.PPS);
    }

    [Fact(DisplayName = "NALUHeader NAL 单元类型 SEI")]
    public void Constructor_SEI()
    {
        // 0x26 = forbidden=0, nal_ref=1, type=6(SEI)
        var header = new NALUHeader(0x26);
        Assert.Equal(NalUnitType.SEI, header.NalUnitType);
        Assert.Equal(1, header.NalRefIdc);
    }
}
