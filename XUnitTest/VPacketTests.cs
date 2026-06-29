using NewLife.Data;
using NewLife.Video;
using Xunit;

namespace XUnitTest;

public class VPacketTests
{
    [Fact(DisplayName = "VPacket 无参构造所有属性为默认值")]
    public void DefaultConstructor_AllPropertiesDefault()
    {
        var p = new VPacket();

        Assert.Null(p.Data);
        Assert.Equal((VideoTypes)0, p.CodecType);
        Assert.Equal(0L, p.PTS);
        Assert.Equal(0L, p.DTS);
        Assert.False(p.IsKeyFrame);
        Assert.Equal(0L, p.FrameIndex);
    }

    [Fact(DisplayName = "VPacket 含参构造正确设置 Data 和 CodecType")]
    public void ParameterizedConstructor_SetsDataAndCodecType()
    {
        var raw = new Byte[] { 1, 2, 3 };
        var data = new ArrayPacket(raw);
        var p = new VPacket(data, VideoTypes.H264);

        Assert.NotNull(p.Data);
        Assert.Equal(raw, p.Data.ToArray());
        Assert.Equal(VideoTypes.H264, p.CodecType);
    }

    [Fact(DisplayName = "VPacket 属性读写正常")]
    public void Properties_ReadWrite()
    {
        var p = new VPacket
        {
            PTS = 1000,
            DTS = 900,
            IsKeyFrame = true,
            FrameIndex = 42,
            CodecType = VideoTypes.H265,
        };

        Assert.Equal(1000L, p.PTS);
        Assert.Equal(900L, p.DTS);
        Assert.True(p.IsKeyFrame);
        Assert.Equal(42L, p.FrameIndex);
        Assert.Equal(VideoTypes.H265, p.CodecType);
    }

    [Fact(DisplayName = "VPacket Data 可设为 null")]
    public void Data_CanBeNull()
    {
        var p = new VPacket { Data = null };
        Assert.Null(p.Data);
    }

    [Fact(DisplayName = "VPacket PTS/DTS 支持负值")]
    public void Timestamps_NegativeValues()
    {
        var p = new VPacket { PTS = -1, DTS = -1 };
        Assert.Equal(-1L, p.PTS);
        Assert.Equal(-1L, p.DTS);
    }

    [Fact(DisplayName = "VPacket FrameIndex 支持大值")]
    public void FrameIndex_LargeValue()
    {
        var p = new VPacket { FrameIndex = Int64.MaxValue };
        Assert.Equal(Int64.MaxValue, p.FrameIndex);
    }
}
