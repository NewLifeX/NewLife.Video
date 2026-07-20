using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class WebPEncoderTests
{
    [Fact(DisplayName = "WebPEncoder RGB24 编码基本验证")]
    public void Encode_RGB24_ReturnsValidWebP()
    {
        var width = 16;
        var height = 16;
        var rgbData = new Byte[width * height * 3];
        // 填充红色
        for (var i = 0; i < width * height; i++)
        {
            rgbData[i * 3] = 255;
            rgbData[i * 3 + 1] = 0;
            rgbData[i * 3 + 2] = 0;
        }

        var result = WebPEncoder.Encode(rgbData, width, height);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        // WebP 签名验证 (RIFF + WEBP)
        Assert.Equal(0x52, result[0]); // 'R'
        Assert.Equal(0x49, result[1]); // 'I'
        Assert.Equal(0x46, result[2]); // 'F'
        Assert.Equal(0x46, result[3]); // 'F'
        Assert.Equal(0x57, result[8]); // 'W'
        Assert.Equal(0x45, result[9]); // 'E'
        Assert.Equal(0x42, result[10]); // 'B'
        Assert.Equal(0x50, result[11]); // 'P'
    }

    [Fact(DisplayName = "WebPEncoder 空参数抛出异常")]
    public void Encode_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WebPEncoder.Encode(null, 10, 10));
    }

    [Fact(DisplayName = "WebPEncoder 无效宽高抛出异常")]
    public void Encode_InvalidDimensions_Throws()
    {
        var data = new Byte[100];
        Assert.Throws<ArgumentOutOfRangeException>(() => WebPEncoder.Encode(data, 0, 10));
    }

    [Fact(DisplayName = "WebPEncoder 数据长度不足抛出异常")]
    public void Encode_InsufficientData_Throws()
    {
        var data = new Byte[10];
        Assert.Throws<ArgumentException>(() => WebPEncoder.Encode(data, 100, 100));
    }

    [Fact(DisplayName = "WebPEncoder 2x2 小尺寸编码")]
    public void Encode_SmallSize_Success()
    {
        var data = new Byte[2 * 2 * 3];
        var result = WebPEncoder.Encode(data, 2, 2);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }
}
