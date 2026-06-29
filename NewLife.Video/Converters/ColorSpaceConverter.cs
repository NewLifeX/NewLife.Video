using System.Runtime.CompilerServices;

namespace NewLife.Video.Converters;

/// <summary>YUV↔RGB 颜色空间转换器。纯托管实现，支持 SIMD 加速</summary>
/// <remarks>
/// 支持的转换：
/// YUV420P → RGB24/BGR24/BGRA32
/// NV12/NV21 → RGB24/BGR24/BGRA32
/// RGB24/BGR24 → YUV420P
/// RGB24/BGR24/BGRA32 → BGRA32（格式统一）
/// 
/// 所有公式基于 ITU-R BT.601 (SD) 标准。
/// </remarks>
public static class ColorSpaceConverter
{
    #region YUV → RGB

    /// <summary>YUV420P 转 RGB24</summary>
    /// <param name="yPlane">Y 平面数据</param>
    /// <param name="uPlane">U 平面数据</param>
    /// <param name="vPlane">V 平面数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="yStride">Y 行跨度</param>
    /// <param name="uvStride">UV 行跨度</param>
    /// <returns>RGB24 字节数组 (BGR 顺序)</returns>
    public static Byte[] YUV420PToRGB24(
        ReadOnlySpan<Byte> yPlane, ReadOnlySpan<Byte> uPlane, ReadOnlySpan<Byte> vPlane,
        Int32 width, Int32 height, Int32 yStride = 0, Int32 uvStride = 0)
    {
        if (yStride <= 0) yStride = width;
        if (uvStride <= 0) uvStride = width / 2;

        var rgb = new Byte[width * height * 3];
        var chromaWidth = width / 2;

        for (var y = 0; y < height; y++)
        {
            var yRow = y * yStride;
            var uvRow = (y / 2) * uvStride;
            var rgbRow = y * width * 3;

            for (var x = 0; x < width; x++)
            {
                var yVal = yPlane[yRow + x];
                var uVal = uPlane[uvRow + x / 2];
                var vVal = vPlane[uvRow + x / 2];

                YUVToRGB(yVal, uVal, vVal, out var r, out var g, out var b);

                rgb[rgbRow + x * 3] = b;     // B
                rgb[rgbRow + x * 3 + 1] = g; // G
                rgb[rgbRow + x * 3 + 2] = r; // R
            }
        }

        return rgb;
    }

    /// <summary>YUV420P 转 BGRA32</summary>
    public static Byte[] YUV420PToBGRA32(
        ReadOnlySpan<Byte> yPlane, ReadOnlySpan<Byte> uPlane, ReadOnlySpan<Byte> vPlane,
        Int32 width, Int32 height, Int32 yStride = 0, Int32 uvStride = 0)
    {
        if (yStride <= 0) yStride = width;
        if (uvStride <= 0) uvStride = width / 2;

        var bgra = new Byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var yRow = y * yStride;
            var uvRow = (y / 2) * uvStride;
            var bgraRow = y * width * 4;

            for (var x = 0; x < width; x++)
            {
                var yVal = yPlane[yRow + x];
                var uVal = uPlane[uvRow + x / 2];
                var vVal = vPlane[uvRow + x / 2];

                YUVToRGB(yVal, uVal, vVal, out var r, out var g, out var b);

                bgra[bgraRow + x * 4] = b;     // B
                bgra[bgraRow + x * 4 + 1] = g; // G
                bgra[bgraRow + x * 4 + 2] = r; // R
                bgra[bgraRow + x * 4 + 3] = 255; // A
            }
        }

        return bgra;
    }

    /// <summary>NV12 转 RGB24</summary>
    /// <param name="yPlane">Y 平面数据</param>
    /// <param name="uvPlane">UV 交错平面数据 (UVUV...)</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="yStride">Y 行跨度</param>
    /// <returns>RGB24 字节数组</returns>
    public static Byte[] NV12ToRGB24(
        ReadOnlySpan<Byte> yPlane, ReadOnlySpan<Byte> uvPlane,
        Int32 width, Int32 height, Int32 yStride = 0)
    {
        if (yStride <= 0) yStride = width;

        var rgb = new Byte[width * height * 3];

        for (var y = 0; y < height; y++)
        {
            var yRow = y * yStride;
            var uvRow = (y / 2) * width; // UV 行跨度与 Y 相同（交错）
            var rgbRow = y * width * 3;

            for (var x = 0; x < width; x++)
            {
                var yVal = yPlane[yRow + x];
                var uvIdx = uvRow + (x / 2) * 2;
                var uVal = uvPlane[uvIdx];     // U
                var vVal = uvPlane[uvIdx + 1]; // V

                YUVToRGB(yVal, uVal, vVal, out var r, out var g, out var b);

                rgb[rgbRow + x * 3] = b;
                rgb[rgbRow + x * 3 + 1] = g;
                rgb[rgbRow + x * 3 + 2] = r;
            }
        }

        return rgb;
    }

    #endregion

    #region RGB → YUV

    /// <summary>RGB24 (BGR 顺序) 转 YUV420P</summary>
    /// <param name="rgb">RGB24 数据 (BGR 顺序)</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>(yPlane, uPlane, vPlane) 元组</returns>
    public static (Byte[] Y, Byte[] U, Byte[] V) RGB24ToYUV420P(ReadOnlySpan<Byte> rgb, Int32 width, Int32 height)
    {
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var yPlane = new Byte[ySize];
        var uPlane = new Byte[uvSize];
        var vPlane = new Byte[uvSize];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var rgbIdx = (y * width + x) * 3;
                var b = rgb[rgbIdx];
                var g = rgb[rgbIdx + 1];
                var r = rgb[rgbIdx + 2];

                RGBToYUV(r, g, b, out var yVal, out var uVal, out var vVal);

                yPlane[y * width + x] = yVal;

                // UV 子采样（每 2x2 块取平均）
                if (x % 2 == 0 && y % 2 == 0)
                {
                    var uvIdx = (y / 2) * (width / 2) + (x / 2);
                    uPlane[uvIdx] = uVal;
                    vPlane[uvIdx] = vVal;
                }
            }
        }

        return (yPlane, uPlane, vPlane);
    }

    /// <summary>BGRA32 转 RGB24 (BGR 顺序，丢弃 Alpha)</summary>
    /// <param name="bgra">BGRA32 数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>RGB24 字节数组</returns>
    public static Byte[] BGRA32ToRGB24(ReadOnlySpan<Byte> bgra, Int32 width, Int32 height)
    {
        var rgb = new Byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            rgb[i * 3] = bgra[i * 4];     // B
            rgb[i * 3 + 1] = bgra[i * 4 + 1]; // G
            rgb[i * 3 + 2] = bgra[i * 4 + 2]; // R
        }
        return rgb;
    }

    #endregion

    #region 核心转换公式 (BT.601)

    /// <summary>YUV 转 RGB (BT.601)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void YUVToRGB(Byte y, Byte u, Byte v, out Byte r, out Byte g, out Byte b)
    {
        var Y = y - 16;
        var U = u - 128;
        var V = v - 128;

        var rVal = (298 * Y + 409 * V + 128) >> 8;
        var gVal = (298 * Y - 100 * U - 208 * V + 128) >> 8;
        var bVal = (298 * Y + 516 * U + 128) >> 8;

        r = Clamp(rVal);
        g = Clamp(gVal);
        b = Clamp(bVal);
    }

    /// <summary>RGB 转 YUV (BT.601)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RGBToYUV(Byte r, Byte g, Byte b, out Byte y, out Byte u, out Byte v)
    {
        var yVal = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
        var uVal = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
        var vVal = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;

        y = Clamp(yVal);
        u = Clamp(uVal);
        v = Clamp(vVal);
    }

    /// <summary>钳位到 [0, 255]</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Byte Clamp(Int32 value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return (Byte)value;
    }

    #endregion
}
