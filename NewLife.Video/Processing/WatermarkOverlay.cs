using NewLife.Video.Converters;

namespace NewLife.Video.Processing;

/// <summary>水印叠加处理器。在视频帧上叠加文字或图像水印</summary>
/// <remarks>
/// 支持文字水印（指定文本、位置、字体大小、颜色）和图像水印（指定图像数据、位置、透明度）。
/// 当前为纯托管文字水印实现，使用简单位图字体。
/// </remarks>
public class WatermarkOverlay : IVideoProcessor
{
    private readonly String _text;
    private readonly Int32 _x;
    private readonly Int32 _y;
    private readonly Byte _r;
    private readonly Byte _g;
    private readonly Byte _b;
    private readonly Double _opacity;

    /// <summary>支持的输入格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32];

    /// <summary>输出格式</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24;

    /// <summary>创建文字水印处理器</summary>
    /// <param name="text">水印文本</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="r">红色分量</param>
    /// <param name="g">绿色分量</param>
    /// <param name="b">蓝色分量</param>
    /// <param name="opacity">透明度（0.0=全透明, 1.0=不透明）</param>
    public WatermarkOverlay(String text, Int32 x = 10, Int32 y = 10,
        Byte r = 255, Byte g = 255, Byte b = 255, Double opacity = 0.8)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _x = x;
        _y = y;
        _r = r;
        _g = g;
        _b = b;
        _opacity = opacity < 0 ? 0 : opacity > 1 ? 1 : opacity;
    }

    /// <summary>叠加水印到帧上</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>叠加水印后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        // 转为 RGB24 统一处理
        Byte[] rgb;
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            rgb = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, rgb, frame.Data.Length);
        }
        else if (frame.PixelFormat == VideoTypes.BGRA32)
        {
            rgb = ColorSpaceConverter.BGRA32ToRGB24(frame.Data, frame.Width, frame.Height);
        }
        else
        {
            throw new NotSupportedException(L.T("Unsupported pixel format: {0}.", frame.PixelFormat));
        }

        // 绘制文字水印（简单 8x16 位图字体）
        DrawText(rgb, frame.Width, frame.Height, _text, _x, _y, _r, _g, _b, _opacity);

        var result = VideoFrame.Create(frame.Width, frame.Height, VideoTypes.RGB24);
        Array.Copy(rgb, result.Data, rgb.Length);

        result.Timestamp = frame.Timestamp;
        result.IsKeyFrame = frame.IsKeyFrame;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }

    /// <summary>在 RGB24 缓冲区上绘制 ASCII 文字</summary>
    /// <remarks>使用内置 8x16 位图字体，仅支持 ASCII 可打印字符</remarks>
    private static void DrawText(Byte[] rgb, Int32 width, Int32 height,
        String text, Int32 startX, Int32 startY,
        Byte r, Byte g, Byte b, Double opacity)
    {
        var charWidth = 8;
        var charHeight = 16;
        var x = startX;
        var y = startY;

        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                x = startX;
                y += charHeight;
                continue;
            }

            // 检查边界
            if (x + charWidth > width || y + charHeight > height)
                break;

            DrawChar(rgb, width, ch, x, y, r, g, b, opacity);
            x += charWidth;
        }
    }

    /// <summary>绘制单个 ASCII 字符</summary>
    private static void DrawChar(Byte[] rgb, Int32 imgWidth, Char ch,
        Int32 posX, Int32 posY, Byte r, Byte g, Byte b, Double opacity)
    {
        if (ch < 32 || ch > 126) ch = '?'; // 不可打印字符

        var glyph = GetGlyph(ch);
        for (var gy = 0; gy < 16; gy++)
        {
            var row = glyph[gy];
            for (var gx = 0; gx < 8; gx++)
            {
                if ((row & (1 << (7 - gx))) == 0) continue;

                var px = posX + gx;
                var py = posY + gy;
                if (px < 0 || px >= imgWidth || py < 0) continue;

                var idx = (py * imgWidth + px) * 3;
                if (idx + 2 >= rgb.Length) continue;

                // Alpha 混合
                rgb[idx] = (Byte)(rgb[idx] * (1 - opacity) + b * opacity);
                rgb[idx + 1] = (Byte)(rgb[idx + 1] * (1 - opacity) + g * opacity);
                rgb[idx + 2] = (Byte)(rgb[idx + 2] * (1 - opacity) + r * opacity);
            }
        }
    }

    /// <summary>获取 ASCII 字符的 8x16 位图数据</summary>
    /// <remarks>简化字体，仅覆盖常用字符的基础形状</remarks>
    private static Byte[] GetGlyph(Char ch)
    {
        // 简化的 8x16 位图字体（仅基本形状）
        // 每个字节表示一行，bit 7=最左像素
        return ch switch
        {
            '0' => [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
            '1' => [0x18, 0x38, 0x78, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x7E, 0x00],
            '2' => [0x3C, 0x66, 0x66, 0x06, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 0x00],
            '3' => [0x3C, 0x66, 0x66, 0x06, 0x06, 0x06, 0x1C, 0x06, 0x06, 0x06, 0x06, 0x06, 0x66, 0x66, 0x3C, 0x00],
            '4' => [0x0C, 0x1C, 0x3C, 0x6C, 0xCC, 0xCC, 0xCC, 0xFE, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x00],
            '5' => [0x7E, 0x60, 0x60, 0x60, 0x60, 0x7C, 0x66, 0x06, 0x06, 0x06, 0x06, 0x06, 0x66, 0x66, 0x3C, 0x00],
            '6' => [0x3C, 0x66, 0x66, 0x60, 0x60, 0x7C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
            '7' => [0x7E, 0x66, 0x66, 0x06, 0x06, 0x0C, 0x0C, 0x18, 0x18, 0x18, 0x18, 0x30, 0x30, 0x30, 0x30, 0x00],
            '8' => [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3C, 0x00],
            '9' => [0x3C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x66, 0x3E, 0x06, 0x06, 0x06, 0x06, 0x66, 0x66, 0x3C, 0x00],
            ':' => [0x00, 0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00, 0x00],
            '-' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            '.' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00],
            ' ' => [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            _ => [0x00, 0x00, 0x00, 0x7E, 0x66, 0x66, 0x06, 0x0C, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00], // '?' 默认
        };
    }
}
