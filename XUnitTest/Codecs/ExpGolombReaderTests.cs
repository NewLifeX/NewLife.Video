using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

/// <summary>Exp-Golomb 位读取器与 H.264 SPS 解析测试</summary>
public class ExpGolombReaderTests
{
    [Theory(DisplayName = "ReadUEG 解码无符号指数哥伦布值")]
    [InlineData(0x80, 0u)]
    [InlineData(0x40, 1u)]
    [InlineData(0x60, 2u)]
    [InlineData(0x20, 3u)]
    [InlineData(0x10, 7u)]
    public void ReadUEG_DecodesValues(Byte data, UInt32 expected)
    {
        var reader = new ExpGolombReader([data]);

        Assert.Equal(expected, reader.ReadUEG());
    }

    [Theory(DisplayName = "ReadEG 解码有符号指数哥伦布值")]
    [InlineData(0x80, 0)]
    [InlineData(0x40, 1)]
    [InlineData(0x60, -1)]
    [InlineData(0x20, 2)]
    [InlineData(0x28, -2)]
    public void ReadEG_DecodesSignedValues(Byte data, Int32 expected)
    {
        var reader = new ExpGolombReader([data]);

        Assert.Equal(expected, reader.ReadEG());
    }

    [Fact(DisplayName = "ReadBits 跨字节边界正确")]
    public void ReadBits_CrossByteBoundary()
    {
        var reader = new ExpGolombReader([0xAB, 0xCD]);

        Assert.Equal(0xAu, reader.ReadBits(4));
        Assert.Equal(0xBu, reader.ReadBits(4));
        Assert.Equal(0xCDu, reader.ReadBits(8));
    }

    [Fact(DisplayName = "ReadBits 跨 32 位工作字边界正确")]
    public void ReadBits_CrossWordBoundary()
    {
        var reader = new ExpGolombReader([0xAB, 0xCD, 0xEF, 0x01, 0x23]);

        Assert.Equal(0xABCu, reader.ReadBits(12));
        Assert.Equal(0xDu, reader.ReadBits(4));
        Assert.Equal(0xEF0123u, reader.ReadBits(24));
    }

    [Fact(DisplayName = "ReadBits 超过 32 位抛出溢出异常")]
    public void ReadBits_Over32Bits_Throws()
    {
        var reader = new ExpGolombReader(new Byte[8]);

        var threw = false;
        try
        {
            reader.ReadBits(33);
        }
        catch (OverflowException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact(DisplayName = "ReadSPS 解析 Baseline 640x480")]
    public void ReadSPS_Baseline640x480()
    {
        // NAL header(0x67) + profile 66 + level 30 + 640x480
        var sps = Convert.FromHexString("6742001EF40501EC80");

        var info = new ExpGolombReader(sps).ReadSPS();

        Assert.Equal(66, info.profileIdc);
        Assert.Equal(30, info.levelIdc);
        Assert.Equal(640, info.width);
        Assert.Equal(480, info.height);
    }

    [Fact(DisplayName = "ReadSPS 解析 High Profile 1920x1080 裁剪")]
    public void ReadSPS_HighProfile1080pWithCrop()
    {
        // NAL header(0x67) + profile 100 + level 40 + 1920x1088，底部裁剪 4 个色度单位得到 1080
        var sps = Convert.FromHexString("67640028ACE501E0089F95");

        var info = new ExpGolombReader(sps).ReadSPS();

        Assert.Equal(100, info.profileIdc);
        Assert.Equal(40, info.levelIdc);
        Assert.Equal(1920, info.width);
        Assert.Equal(1080, info.height);
    }
}
