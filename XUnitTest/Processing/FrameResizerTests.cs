using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FrameResizerTests
{
    [Fact(DisplayName = "FrameResizer缩放尺寸正确")]
    public void Resize_CorrectDimensions()
    {
        var resizer = new FrameResizer(32, 24);
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);

        var output = resizer.ProcessFrame(input);

        Assert.Equal(32, output.Width);
        Assert.Equal(24, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "FrameResizer保持时间戳和帧号")]
    public void Resize_PreservesMetadata()
    {
        var resizer = new FrameResizer(16, 16);
        var input = VideoFrame.Create(64, 64, VideoTypes.RGB24);
        input.Timestamp = 12345;
        input.FrameIndex = 99;
        input.IsKeyFrame = true;

        var output = resizer.ProcessFrame(input);

        Assert.Equal(12345, output.Timestamp);
        Assert.Equal(99, output.FrameIndex);
        Assert.True(output.IsKeyFrame);
    }

    [Fact(DisplayName = "FrameResizer放大到更大尺寸")]
    public void Resize_Upscale_Correct()
    {
        var resizer = new FrameResizer(64, 64);
        var input = VideoFrame.Create(16, 16, VideoTypes.RGB24);

        var output = resizer.ProcessFrame(input);

        Assert.Equal(64, output.Width);
        Assert.Equal(64, output.Height);
    }

    [Fact(DisplayName = "FrameResizer目标尺寸<=0抛异常")]
    public void Constructor_InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameResizer(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameResizer(100, -1));
    }
}
