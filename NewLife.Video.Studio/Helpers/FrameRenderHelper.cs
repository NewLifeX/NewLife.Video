using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NewLife.Video.Processing;

namespace NewLife.Video.Studio.Helpers;

/// <summary>帧渲染辅助类，将 VideoFrame 转换为 Avalonia WriteableBitmap</summary>
public static class FrameRenderHelper
{
    /// <summary>将 VideoFrame 渲染为 WriteableBitmap</summary>
    /// <param name="frame">视频帧</param>
    /// <returns>WriteableBitmap，失败返回 null</returns>
    public static WriteableBitmap? ToBitmap(NewLife.Video.VideoFrame frame)
    {
        if (frame == null || frame.Width <= 0 || frame.Height <= 0) return null;

        try
        {
            var bgra = VideoFrameRenderer.ToBgra32(frame);
            var bitmap = new WriteableBitmap(
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            using var locked = bitmap.Lock();
            unsafe
            {
                var dst = (Byte*)locked.Address;
                fixed (Byte* pSrc = bgra)
                {
                    System.Buffer.MemoryCopy(pSrc, dst, bgra.Length, bgra.Length);
                }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>将 BGRA32 字节数组写入 WriteableBitmap</summary>
    /// <param name="bitmap">目标位图</param>
    /// <param name="data">BGRA32 像素数据</param>
    public static void WriteToBitmap(WriteableBitmap bitmap, Byte[] data)
    {
        using var locked = bitmap.Lock();
        unsafe
        {
            var dst = (Byte*)locked.Address;
            fixed (Byte* pSrc = data)
            {
                System.Buffer.MemoryCopy(pSrc, dst, data.Length, data.Length);
            }
        }
    }
}
