using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class PngEncoderTests
{
    [Fact(DisplayName = "PngEncoder RGB24 编码基本验证")]
    public void Encode_RGB24_ReturnsValidPng()
    {
        var width = 4;
        var height = 4;
        var rgbData = new Byte[width * height * 3];
        // 填充纯蓝色
        for (var i = 0; i < width * height; i++)
        {
            rgbData[i * 3] = 0;      // R
            rgbData[i * 3 + 1] = 0;  // G
            rgbData[i * 3 + 2] = 255; // B
        }

        var result = PngEncoder.Encode(rgbData, width, height);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        // PNG 签名验证
        Assert.Equal(137, result[0]); // 0x89
        Assert.Equal(80, result[1]);  // 'P'
        Assert.Equal(78, result[2]);  // 'N'
        Assert.Equal(71, result[3]);  // 'G'
    }

    [Fact(DisplayName = "PngEncoder 空参数抛出异常")]
    public void Encode_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PngEncoder.Encode(null, 10, 10));
    }

    [Fact(DisplayName = "PngEncoder 无效宽高抛出异常")]
    public void Encode_InvalidDimensions_Throws()
    {
        var data = new Byte[100];
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Encode(data, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Encode(data, 10, 0));
    }

    [Fact(DisplayName = "PngEncoder 数据长度不足抛出异常")]
    public void Encode_InsufficientData_Throws()
    {
        var data = new Byte[10];
        Assert.Throws<ArgumentException>(() => PngEncoder.Encode(data, 100, 100));
    }

    [Fact(DisplayName = "PngEncoder 不同尺寸编码")]
    public void Encode_VariousSizes_Success()
    {
        var sizes = new[] { (1, 1), (2, 2), (8, 8), (16, 16), (32, 16) };
        foreach (var (w, h) in sizes)
        {
            var data = new Byte[w * h * 3];
            var result = PngEncoder.Encode(data, w, h);
            Assert.NotNull(result);
            Assert.True(result.Length > 0, $"Failed for size {w}x{h}");
        }
    }
}
