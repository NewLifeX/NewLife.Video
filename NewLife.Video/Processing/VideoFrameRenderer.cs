using NewLife.Data;

namespace NewLife.Video.Processing;

/// <summary>纯托管帧渲染器。将 VideoFrame 渲染到目标缓冲区，零外部依赖</summary>
/// <remarks>
/// 提供 VideoFrame → BGRA32 像素数据的转换，可与 WPF WriteableBitmap、WinForms Bitmap、
/// 或任何接受 BGRA32 像素数据的 UI 框架集成。
/// 配合 FfmpegPlayer 的 FrameDecoded 事件使用，实现纯托管渲染管线。
/// </remarks>
public static class VideoFrameRenderer
{
    /// <summary>将 VideoFrame 转换为 BGRA32 像素数据（预乘 Alpha）</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>BGRA32 字节数组（每像素 4 字节，stride = width * 4）</returns>
    public static Byte[] ToBgra32(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        var width = frame.Width;
        var height = frame.Height;
        var pixelCount = width * height;
        var result = new Byte[pixelCount * 4];

        switch (frame.PixelFormat)
        {
            case VideoTypes.RGB24:
                ConvertRgb24ToBgra32(frame.Data, result, pixelCount);
                break;

            case VideoTypes.BGRA32:
                // 已是 BGRA32，直接复制
                Array.Copy(frame.Data, result, Math.Min(frame.Data.Length, result.Length));
                break;

            case VideoTypes.YUV420P:
            case VideoTypes.NV12:
            case VideoTypes.NV21:
                {
                    // 先转为 RGB24，再转 BGRA32
                    Byte[] rgb;
                    if (frame.PixelFormat == VideoTypes.YUV420P)
                        rgb = PixelFormatConverter.YUV420PToRGB24(frame.Data, width, height);
                    else if (frame.PixelFormat == VideoTypes.NV12)
                        rgb = PixelFormatConverter.NV12ToRGB24(frame.Data, width, height);
                    else
                        rgb = PixelFormatConverter.NV21ToRGB24(frame.Data, width, height);

                    if (rgb != null)
                        ConvertRgb24ToBgra32(rgb, result, pixelCount);
                    break;
                }

            default:
                // 回退：尝试作为 RGB24 处理
                ConvertRgb24ToBgra32(frame.Data, result, pixelCount);
                break;
        }

        return result;
    }

    /// <summary>将 VideoFrame 转换为 BGRA32 像素数据，并写入目标缓冲区</summary>
    /// <param name="frame">视频帧</param>
    /// <param name="bgraBuffer">目标缓冲区（至少 width * height * 4 字节）</param>
    /// <param name="offset">目标缓冲区起始偏移</param>
    /// <returns>写入的字节数</returns>
    public static Int32 RenderToBuffer(VideoFrame frame, Byte[] bgraBuffer, Int32 offset = 0)
    {
        var data = ToBgra32(frame);
        if (data == null || data.Length == 0) return 0;

        Array.Copy(data, 0, bgraBuffer, offset, data.Length);
        return data.Length;
    }

    /// <summary>创建兼容 WPF WriteableBitmap 的 BGRA32 缓冲区</summary>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>BGRA32 字节数组（已清零）</returns>
    public static Byte[] CreateBuffer(Int32 width, Int32 height)
    {
        return new Byte[width * height * 4];
    }

    /// <summary>将 RGB24 转换为 BGRA32（Alpha=255）</summary>
    private static void ConvertRgb24ToBgra32(Byte[] rgb, Byte[] bgra, Int32 pixelCount)
    {
        for (var i = 0; i < pixelCount; i++)
        {
            var si = i * 3;
            var di = i * 4;
            bgra[di] = rgb[si + 2];     // B = R
            bgra[di + 1] = rgb[si + 1]; // G = G
            bgra[di + 2] = rgb[si];     // R = B
            bgra[di + 3] = 255;         // A
        }
    }

    /// <summary>提取指定坐标的像素颜色（BGRA 格式）</summary>
    /// <param name="bgraData">BGRA32 像素数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="x">像素 X 坐标</param>
    /// <param name="y">像素 Y 坐标</param>
    /// <returns>BGRA 颜色值（B, G, R, A）</returns>
    public static (Byte b, Byte g, Byte r, Byte a) GetPixel(Byte[] bgraData, Int32 width, Int32 x, Int32 y)
    {
        var offset = (y * width + x) * 4;
        if (offset < 0 || offset + 3 >= bgraData.Length)
            return (0, 0, 0, 0);
        return (bgraData[offset], bgraData[offset + 1], bgraData[offset + 2], bgraData[offset + 3]);
    }

    /// <summary>设置指定坐标的像素颜色（BGRA 格式）</summary>
    /// <param name="bgraData">BGRA32 像素数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="x">像素 X 坐标</param>
    /// <param name="y">像素 Y 坐标</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="r">红色分量</param>
    /// <param name="a">Alpha 分量（默认 255）</param>
    public static void SetPixel(Byte[] bgraData, Int32 width, Int32 x, Int32 y, Byte b, Byte g, Byte r, Byte a = 255)
    {
        var offset = (y * width + x) * 4;
        if (offset < 0 || offset + 3 >= bgraData.Length) return;
        bgraData[offset] = b;
        bgraData[offset + 1] = g;
        bgraData[offset + 2] = r;
        bgraData[offset + 3] = a;
    }
}
