using NewLife.Video.Converters;
using Xunit;

namespace XUnitTest;

public class ColorSpaceConverterTests
{
    #region YUV → RGB

    [Fact(DisplayName = "YUV420P转RGB24黑色正确")]
    public void YUV420PToRGB24_Black_Correct()
    {
        // Y=16, U=128, V=128 → R=0, G=0, B=0 (黑色)
        var yPlane = new Byte[640 * 480];
        var uPlane = new Byte[320 * 240];
        var vPlane = new Byte[320 * 240];

        Array.Fill(yPlane, (Byte)16);
        Array.Fill(uPlane, (Byte)128);
        Array.Fill(vPlane, (Byte)128);

        var rgb = ColorSpaceConverter.YUV420PToRGB24(yPlane, uPlane, vPlane, 640, 480);

        Assert.Equal(640 * 480 * 3, rgb.Length);
        // 所有像素应为黑色
        Assert.Equal(0, rgb[0]);
        Assert.Equal(0, rgb[1]);
        Assert.Equal(0, rgb[2]);
    }

    [Fact(DisplayName = "YUV420P转RGB24白色正确")]
    public void YUV420PToRGB24_White_Correct()
    {
        var yPlane = new Byte[320 * 240];
        var uPlane = new Byte[160 * 120];
        var vPlane = new Byte[160 * 120];

        Array.Fill(yPlane, (Byte)235); // 白色 Y ~235
        Array.Fill(uPlane, (Byte)128);
        Array.Fill(vPlane, (Byte)128);

        var rgb = ColorSpaceConverter.YUV420PToRGB24(yPlane, uPlane, vPlane, 320, 240);

        // 应为灰白色，RGB 三通道接近
        Assert.True(rgb[0] > 200, $"Expected B > 200, got {rgb[0]}");
        Assert.True(rgb[1] > 200, $"Expected G > 200, got {rgb[1]}");
        Assert.True(rgb[2] > 200, $"Expected R > 200, got {rgb[2]}");
    }

    [Fact(DisplayName = "YUV420P转RGB24红色正确")]
    public void YUV420PToRGB24_Red_Correct()
    {
        // V=240 (高), U=128 → 偏红
        var yPlane = new Byte[16 * 16];
        var uPlane = new Byte[8 * 8];
        var vPlane = new Byte[8 * 8];

        Array.Fill(yPlane, (Byte)128);
        Array.Fill(uPlane, (Byte)128);
        Array.Fill(vPlane, (Byte)240);

        var rgb = ColorSpaceConverter.YUV420PToRGB24(yPlane, uPlane, vPlane, 16, 16);

        // R 通道应明显高于 G 和 B
        Assert.True(rgb[2] > rgb[0], $"R({rgb[2]}) should be > B({rgb[0]})");
        Assert.True(rgb[2] > rgb[1], $"R({rgb[2]}) should be > G({rgb[1]})");
    }

    [Fact(DisplayName = "YUV420P转BGRA32输出正确尺寸和Alpha")]
    public void YUV420PToBGRA32_CorrectSize()
    {
        var yPlane = new Byte[64 * 48];
        var uPlane = new Byte[32 * 24];
        var vPlane = new Byte[32 * 24];

        Array.Fill(yPlane, (Byte)128);
        Array.Fill(uPlane, (Byte)128);
        Array.Fill(vPlane, (Byte)128);

        var bgra = ColorSpaceConverter.YUV420PToBGRA32(yPlane, uPlane, vPlane, 64, 48);

        Assert.Equal(64 * 48 * 4, bgra.Length);
        // Alpha 通道应为 255
        Assert.Equal(255, bgra[3]);
    }

    [Fact(DisplayName = "NV12转RGB24正确")]
    public void NV12ToRGB24_Correct()
    {
        var width = 32;
        var height = 32;
        var yPlane = new Byte[width * height];
        var uvPlane = new Byte[(width / 2) * height]; // UV 交错

        Array.Fill(yPlane, (Byte)16);
        Array.Fill(uvPlane, (Byte)128);

        var rgb = ColorSpaceConverter.NV12ToRGB24(yPlane, uvPlane, width, height);

        Assert.Equal(width * height * 3, rgb.Length);
    }

    #endregion

    #region RGB → YUV

    [Fact(DisplayName = "RGB24转YUV420P黑色正确")]
    public void RGB24ToYUV420P_Black_Correct()
    {
        var rgb = new Byte[16 * 16 * 3]; // 默认全 0 = 黑色

        var (y, u, v) = ColorSpaceConverter.RGB24ToYUV420P(rgb, 16, 16);

        Assert.Equal(16 * 16, y.Length);
        Assert.Equal(8 * 8, u.Length);
        Assert.Equal(8 * 8, v.Length);

        // 黑色 → Y≈16, U≈128, V≈128
        Assert.True(y[0] <= 20, $"Y should be ~16, got {y[0]}");
    }

    [Fact(DisplayName = "RGB24转YUV420P白色正确")]
    public void RGB24ToYUV420P_White_Correct()
    {
        var rgb = new Byte[8 * 8 * 3];
        Array.Fill(rgb, (Byte)255); // 全部 255 = 白色

        var (y, u, v) = ColorSpaceConverter.RGB24ToYUV420P(rgb, 8, 8);

        // 白色 → Y≈235
        Assert.True(y[0] >= 220, $"Y should be ~235, got {y[0]}");
    }

    [Fact(DisplayName = "RGB24转YUV420P红色正确")]
    public void RGB24ToYUV420P_Red_Correct()
    {
        var rgb = new Byte[8 * 8 * 3];
        // 填充红色: B=0, G=0, R=255
        for (var i = 0; i < 8 * 8; i++)
        {
            rgb[i * 3] = 0;     // B
            rgb[i * 3 + 1] = 0; // G
            rgb[i * 3 + 2] = 255; // R
        }

        var (y, u, v) = ColorSpaceConverter.RGB24ToYUV420P(rgb, 8, 8);

        // 红色 → V 分量应偏高
        Assert.True(v[0] > 130, $"V should be >128 for red, got {v[0]}");
    }

    #endregion

    #region 核心转换公式

    [Fact(DisplayName = "YUV↔RGB往返一致性")]
    public void YUV_RGB_RoundTrip_Consistent()
    {
        // 测试几组典型颜色
        var testColors = new (Byte R, Byte G, Byte B)[]
        {
            (0, 0, 0),
            (255, 255, 255),
            (255, 0, 0),
            (0, 255, 0),
            (0, 0, 255),
            (128, 128, 128),
        };

        foreach (var (r, g, b) in testColors)
        {
            ColorSpaceConverter.RGBToYUV(r, g, b, out var y, out var u, out var v);
            ColorSpaceConverter.YUVToRGB(y, u, v, out var r2, out var g2, out var b2);

            // 允许 ±3 的量化误差
            Assert.True(Math.Abs(r - r2) <= 3, $"R: {r} → {r2}");
            Assert.True(Math.Abs(g - g2) <= 3, $"G: {g} → {g2}");
            Assert.True(Math.Abs(b - b2) <= 3, $"B: {b} → {b2}");
        }
    }

    [Fact(DisplayName = "Clamp钳位正确")]
    public void YUVToRGB_Clamp_Correct()
    {
        // 极端 YUV 值不应溢出
        ColorSpaceConverter.YUVToRGB(0, 0, 0, out var r1, out var g1, out var b1);
        Assert.InRange(r1, (Byte)0, (Byte)255);
        Assert.InRange(g1, (Byte)0, (Byte)255);
        Assert.InRange(b1, (Byte)0, (Byte)255);

        ColorSpaceConverter.YUVToRGB(255, 255, 255, out var r2, out var g2, out var b2);
        Assert.InRange(r2, (Byte)0, (Byte)255);
        Assert.InRange(g2, (Byte)0, (Byte)255);
        Assert.InRange(b2, (Byte)0, (Byte)255);
    }

    #endregion

    #region 格式转换

    [Fact(DisplayName = "BGRA32转RGB24丢弃Alpha正确")]
    public void BGRA32ToRGB24_DropsAlpha()
    {
        var bgra = new Byte[]
        {
            10, 20, 30, 255,  // B=10, G=20, R=30, A=255
            40, 50, 60, 128,  // B=40, G=50, R=60, A=128
        };

        var rgb = ColorSpaceConverter.BGRA32ToRGB24(bgra, 2, 1);

        Assert.Equal(6, rgb.Length);
        Assert.Equal(10, rgb[0]); // B
        Assert.Equal(20, rgb[1]); // G
        Assert.Equal(30, rgb[2]); // R
        Assert.Equal(40, rgb[3]); // B
        Assert.Equal(50, rgb[4]); // G
        Assert.Equal(60, rgb[5]); // R
    }

    #endregion
}
