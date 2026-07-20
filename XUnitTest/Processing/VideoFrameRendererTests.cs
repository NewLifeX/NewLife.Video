using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class VideoFrameRendererTests
{
    [Fact(DisplayName = "VideoFrameRenderer RGB24 帧渲染")]
    public void ToBgra32_RGB24Frame_ReturnsBGRA32()
    {
        var frame = VideoFrame.Create(16, 16, VideoTypes.RGB24);
        var result = VideoFrameRenderer.ToBgra32(frame);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // BGRA32 = 4 字节/像素
        Assert.Equal(16 * 16 * 4, result.Length);
    }

    [Fact(DisplayName = "VideoFrameRenderer YUV420P 帧渲染")]
    public void ToBgra32_YUV420PFrame_ReturnsBGRA32()
    {
        var width = 16;
        var height = 16;
        var ySize = width * height;
        var uvSize = (width / 2) * (height / 2);
        var data = new Byte[ySize + uvSize * 2];

        var frame = new VideoFrame
        {
            Width = width,
            Height = height,
            PixelFormat = VideoTypes.YUV420P,
            Data = data,
            YOffset = 0,
            UOffset = ySize,
            VOffset = ySize + uvSize,
            YStride = width,
            UVStride = width / 2,
        };

        var result = VideoFrameRenderer.ToBgra32(frame);
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(width * height * 4, result.Length);
    }

    [Fact(DisplayName = "VideoFrameRenderer 空帧抛出异常")]
    public void ToBgra32_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VideoFrameRenderer.ToBgra32(null));
    }

    [Fact(DisplayName = "VideoFrameRenderer 空数据抛出异常")]
    public void ToBgra32_EmptyData_Throws()
    {
        var frame = new VideoFrame
        {
            Width = 16,
            Height = 16,
            PixelFormat = VideoTypes.RGB24,
            Data = null,
        };
        Assert.Throws<ArgumentNullException>(() => VideoFrameRenderer.ToBgra32(frame));
    }

    [Fact(DisplayName = "VideoFrameRenderer 不同尺寸渲染")]
    public void ToBgra32_VariousSizes_Success()
    {
        var sizes = new[] { (1, 1), (2, 2), (8, 8), (32, 16) };
        foreach (var (w, h) in sizes)
        {
            var frame = VideoFrame.Create(w, h, VideoTypes.RGB24);
            var result = VideoFrameRenderer.ToBgra32(frame);
            Assert.NotNull(result);
            Assert.True(result.Length == w * h * 4, $"Failed for size {w}x{h}");
        }
    }
}
