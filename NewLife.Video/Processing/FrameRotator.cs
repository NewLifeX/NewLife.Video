using NewLife.Video.Converters;

namespace NewLife.Video.Processing;

/// <summary>帧旋转与翻转处理器</summary>
/// <remarks>
/// 支持 90°/180°/270° 旋转和水平/垂直镜像翻转。
/// 支持 RGB24、BGRA32、YUV420P 格式。
/// </remarks>
public class FrameRotator : IVideoProcessor
{
    /// <summary>旋转角度枚举</summary>
    public enum RotationAngle
    {
        /// <summary>0°（不旋转）</summary>
        None = 0,

        /// <summary>顺时针 90°</summary>
        Clockwise90 = 90,

        /// <summary>180°</summary>
        Clockwise180 = 180,

        /// <summary>顺时针 270°（逆时针 90°）</summary>
        Clockwise270 = 270,
    }

    private readonly RotationAngle _angle;
    private readonly Boolean _flipHorizontal;
    private readonly Boolean _flipVertical;

    /// <summary>支持的输入格式</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24, VideoTypes.BGRA32, VideoTypes.YUV420P];

    /// <summary>输出格式</summary>
    public VideoTypes OutputFormat => VideoTypes.RGB24;

    /// <summary>创建帧旋转处理器</summary>
    /// <param name="angle">旋转角度</param>
    /// <param name="flipHorizontal">水平翻转</param>
    /// <param name="flipVertical">垂直翻转</param>
    public FrameRotator(RotationAngle angle = RotationAngle.None, Boolean flipHorizontal = false, Boolean flipVertical = false)
    {
        _angle = angle;
        _flipHorizontal = flipHorizontal;
        _flipVertical = flipVertical;
    }

    /// <summary>旋转帧</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>旋转后的帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        // 转为 RGB24 统一处理
        Byte[] rgb;
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            rgb = frame.Data;
        }
        else if (frame.PixelFormat == VideoTypes.YUV420P)
        {
            rgb = ColorSpaceConverter.YUV420PToRGB24(
                frame.GetYPlane(), frame.GetUPlane(), frame.GetVPlane(),
                frame.Width, frame.Height, frame.YStride, frame.UVStride);
        }
        else if (frame.PixelFormat == VideoTypes.BGRA32)
        {
            rgb = ColorSpaceConverter.BGRA32ToRGB24(frame.Data, frame.Width, frame.Height);
        }
        else
        {
            throw new NotSupportedException($"不支持的像素格式: {frame.PixelFormat}");
        }

        // 旋转变换
        var (rotated, outWidth, outHeight) = RotatePixels(rgb, frame.Width, frame.Height);

        // 翻转变换
        if (_flipHorizontal || _flipVertical)
            FlipPixels(rotated, outWidth, outHeight, _flipHorizontal, _flipVertical);

        var result = VideoFrame.Create(outWidth, outHeight, VideoTypes.RGB24);
        Array.Copy(rotated, result.Data, rotated.Length);

        result.Timestamp = frame.Timestamp;
        result.IsKeyFrame = frame.IsKeyFrame;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }

    private (Byte[] Pixels, Int32 Width, Int32 Height) RotatePixels(Byte[] src, Int32 width, Int32 height)
    {
        var outWidth = width;
        var outHeight = height;

        switch (_angle)
        {
            case RotationAngle.Clockwise90:
            case RotationAngle.Clockwise270:
                outWidth = height;
                outHeight = width;
                break;
        }

        var dst = new Byte[outWidth * outHeight * 3];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var srcIdx = (y * width + x) * 3;
                var b = src[srcIdx];
                var g = src[srcIdx + 1];
                var r = src[srcIdx + 2];

                Int32 dstX, dstY;
                switch (_angle)
                {
                    case RotationAngle.Clockwise90:
                        dstX = height - 1 - y;
                        dstY = x;
                        break;
                    case RotationAngle.Clockwise180:
                        dstX = width - 1 - x;
                        dstY = height - 1 - y;
                        break;
                    case RotationAngle.Clockwise270:
                        dstX = y;
                        dstY = width - 1 - x;
                        break;
                    default:
                        dstX = x;
                        dstY = y;
                        break;
                }

                var dstIdx = (dstY * outWidth + dstX) * 3;
                dst[dstIdx] = b;
                dst[dstIdx + 1] = g;
                dst[dstIdx + 2] = r;
            }
        }

        return (dst, outWidth, outHeight);
    }

    private static void FlipPixels(Byte[] pixels, Int32 width, Int32 height, Boolean flipH, Boolean flipV)
    {
        var temp = new Byte[width * 3];

        if (flipH)
        {
            for (var y = 0; y < height; y++)
            {
                var row = y * width * 3;
                // 复制一行到临时缓冲区
                Array.Copy(pixels, row, temp, 0, width * 3);
                // 反向写回
                for (var x = 0; x < width; x++)
                {
                    var srcIdx = (width - 1 - x) * 3;
                    var dstIdx = row + x * 3;
                    pixels[dstIdx] = temp[srcIdx];
                    pixels[dstIdx + 1] = temp[srcIdx + 1];
                    pixels[dstIdx + 2] = temp[srcIdx + 2];
                }
            }
        }

        if (flipV)
        {
            for (var y = 0; y < height / 2; y++)
            {
                var row1 = y * width * 3;
                var row2 = (height - 1 - y) * width * 3;
                // 交换两行
                Array.Copy(pixels, row1, temp, 0, width * 3);
                Array.Copy(pixels, row2, pixels, row1, width * 3);
                Array.Copy(temp, 0, pixels, row2, width * 3);
            }
        }
    }
}
