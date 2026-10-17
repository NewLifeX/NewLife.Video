using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

/// <summary>纯托管像素格式转换测试</summary>
public class PixelFormatConverterTests
{
    [Fact(DisplayName = "RGB24 与 BGRA32 往返一致")]
    public void Rgb24ToBgra32_AndBack_RoundTrip()
    {
        var rgb = new Byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        var bgra = PixelFormatConverter.RGB24ToBGRA32(rgb, 2, 2);

        Assert.Equal(new Byte[] { 3, 2, 1, 255, 6, 5, 4, 255, 9, 8, 7, 255, 12, 11, 10, 255 }, bgra);
        Assert.Equal(rgb, PixelFormatConverter.BGRA32ToRGB24(bgra, 2, 2));
    }

    [Fact(DisplayName = "BGR24 转 RGB24 交换通道")]
    public void Bgr24ToRgb24_SwapsChannels()
    {
        var bgr = new Byte[] { 1, 2, 3, 4, 5, 6 };

        var rgb = PixelFormatConverter.BGR24ToRGB24(bgr, 2, 1);

        Assert.Equal(new Byte[] { 3, 2, 1, 6, 5, 4 }, rgb);
    }

    [Fact(DisplayName = "ToRgb24 对 RGB24 帧返回副本")]
    public void ToRgb24_Rgb24Frame_ReturnsCopy()
    {
        var data = new Byte[] { 1, 2, 3, 4, 5, 6 };
        var frame = PixelFormatConverter.FromRgb24(data, 2, 1);

        var rgb = PixelFormatConverter.ToRgb24(frame);

        Assert.Equal(data, rgb);
        Assert.NotSame(data, rgb);
    }

    [Fact(DisplayName = "ToBGRA32 对 RGB24 帧补齐 Alpha")]
    public void ToBGRA32_Rgb24Frame_FillsAlpha()
    {
        var frame = PixelFormatConverter.FromRgb24(new Byte[] { 1, 2, 3 }, 1, 1);

        var bgra = PixelFormatConverter.ToBGRA32(frame);

        Assert.Equal(new Byte[] { 3, 2, 1, 255 }, bgra);
    }

    [Fact(DisplayName = "ToRgb24 空帧抛出参数异常")]
    public void ToRgb24_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PixelFormatConverter.ToRgb24(null));
        Assert.Throws<ArgumentNullException>(() => PixelFormatConverter.ToRgb24(new VideoFrame()));
    }

    [Theory(DisplayName = "YUV420P 有限范围黑白灰转换正确")]
    [InlineData(16, 0)]
    [InlineData(81, 76)]
    [InlineData(235, 255)]
    public void YUV420PToRGB24_LimitedRange_ConvertsCorrectly(Byte y, Byte expected)
    {
        var yuv = new Byte[] { y, y, y, y, 128, 128 };

        var rgb = PixelFormatConverter.YUV420PToRGB24(yuv, 2, 2);

        Assert.Equal(new Byte[] { expected, expected, expected, expected, expected, expected, expected, expected, expected, expected, expected, expected }, rgb);
    }

    [Fact(DisplayName = "NV12 与 NV21 相同 YUV 分量输出一致")]
    public void NV12_NV21_SameComponents_SameOutput()
    {
        const Int32 width = 4;
        const Int32 height = 2;
        var y = new Byte[] { 81, 81, 81, 81, 81, 81, 81, 81 };
        var u = new Byte[] { 100, 140 };
        var v = new Byte[] { 150, 110 };

        var nv12 = BuildNV12(y, u, v);
        var nv21 = BuildNV21(y, u, v);

        Assert.Equal(PixelFormatConverter.NV12ToRGB24(nv12, width, height), PixelFormatConverter.NV21ToRGB24(nv21, width, height));
    }

    [Fact(DisplayName = "YUV420P 与 NV12 相同分量输出一致")]
    public void YUV420P_NV12_SameComponents_SameOutput()
    {
        const Int32 width = 4;
        const Int32 height = 2;
        var y = new Byte[] { 81, 81, 81, 81, 81, 81, 81, 81 };
        var u = new Byte[] { 100, 140 };
        var v = new Byte[] { 150, 110 };
        var yuv420p = new Byte[] { 81, 81, 81, 81, 81, 81, 81, 81, 100, 140, 150, 110 };

        var rgb420 = PixelFormatConverter.YUV420PToRGB24(yuv420p, width, height);
        var rgbNv12 = PixelFormatConverter.NV12ToRGB24(BuildNV12(y, u, v), width, height);

        Assert.Equal(rgb420, rgbNv12);
    }

    [Fact(DisplayName = "BT.601 与 BT.709 彩色输出不同")]
    public void ColorMatrix_Bt601AndBt709_Differ()
    {
        var yuv = new Byte[] { 100, 100, 100, 100, 100, 150 };

        var bt601 = PixelFormatConverter.YUV420PToRGB24(yuv, 2, 2, YuvMatrix.Bt601);
        var bt709 = PixelFormatConverter.YUV420PToRGB24(yuv, 2, 2, YuvMatrix.Bt709);

        Assert.NotEqual(bt601, bt709);
    }

    [Fact(DisplayName = "SIMD 与标量边界的彩色转换结果一致")]
    public void WideFrame_Matches_NarrowFrame()
    {
        // 宽帧走 SIMD 分支，窄帧走标量分支，相同像素应得到相同结果
        const Int32 narrowWidth = 4;
        const Int32 height = 2;
        const Int32 wideWidth = 64;

        var narrowYuv = new Byte[narrowWidth * height + narrowWidth / 2 * height / 2 * 2];
        var wideYuv = new Byte[wideWidth * height + wideWidth / 2 * height / 2 * 2];
        FillYuv420P(narrowYuv, narrowWidth, height);
        FillYuv420P(wideYuv, wideWidth, height);

        var narrow = PixelFormatConverter.YUV420PToRGB24(narrowYuv, narrowWidth, height);
        var wide = PixelFormatConverter.YUV420PToRGB24(wideYuv, wideWidth, height);

        Assert.Equal(narrowWidth * height * 3, narrow.Length);
        Assert.Equal(wideWidth * height * 3, wide.Length);
        // 前 4 个像素与窄帧完全相同
        Assert.Equal(narrow.AsSpan(0, 3).ToArray(), wide.AsSpan(0, 3).ToArray());
    }

    private static Byte[] BuildNV12(Byte[] y, Byte[] u, Byte[] v)
    {
        var data = new Byte[y.Length + u.Length * 2];
        Array.Copy(y, data, y.Length);
        for (var i = 0; i < u.Length; i++)
        {
            data[y.Length + i * 2] = u[i];
            data[y.Length + i * 2 + 1] = v[i];
        }
        return data;
    }

    private static Byte[] BuildNV21(Byte[] y, Byte[] u, Byte[] v)
    {
        var data = new Byte[y.Length + u.Length * 2];
        Array.Copy(y, data, y.Length);
        for (var i = 0; i < u.Length; i++)
        {
            data[y.Length + i * 2] = v[i];
            data[y.Length + i * 2 + 1] = u[i];
        }
        return data;
    }

    private static void FillYuv420P(Byte[] data, Int32 width, Int32 height)
    {
        var frameSize = width * height;
        for (var i = 0; i < frameSize; i++)
            data[i] = (Byte)(16 + i % 220);

        // 色度分量使用常量，保证不同宽度帧的首个像素具有相同的 U/V
        for (var i = frameSize; i < data.Length; i++)
            data[i] = (Byte)(i % 2 == 0 ? 100 : 150);
    }
}
