using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FrameRotatorTests
{
    [Fact(DisplayName = "FrameRotator顺时针90度旋转尺寸互换")]
    public void Rotate90_SwapsDimensions()
    {
        var rotator = new FrameRotator(FrameRotator.RotationAngle.Clockwise90);
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);

        var output = rotator.ProcessFrame(input);

        Assert.Equal(48, output.Width);  // 宽高互换
        Assert.Equal(64, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "FrameRotator 180度旋转尺寸不变")]
    public void Rotate180_SameDimensions()
    {
        var rotator = new FrameRotator(FrameRotator.RotationAngle.Clockwise180);
        var input = VideoFrame.Create(32, 24, VideoTypes.RGB24);

        var output = rotator.ProcessFrame(input);

        Assert.Equal(32, output.Width);
        Assert.Equal(24, output.Height);
    }

    [Fact(DisplayName = "FrameRotator不旋转尺寸不变")]
    public void RotateNone_SameDimensions()
    {
        var rotator = new FrameRotator(FrameRotator.RotationAngle.None);
        var input = VideoFrame.Create(16, 16, VideoTypes.RGB24);

        var output = rotator.ProcessFrame(input);

        Assert.Equal(16, output.Width);
        Assert.Equal(16, output.Height);
    }

    [Fact(DisplayName = "FrameRotator保持元数据")]
    public void Rotate_PreservesMetadata()
    {
        var rotator = new FrameRotator(FrameRotator.RotationAngle.Clockwise90);
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        input.Timestamp = 500;
        input.FrameIndex = 10;
        input.IsKeyFrame = true;

        var output = rotator.ProcessFrame(input);

        Assert.Equal(500, output.Timestamp);
        Assert.Equal(10, output.FrameIndex);
        Assert.True(output.IsKeyFrame);
    }

    [Fact(DisplayName = "FrameRotator水平翻转")]
    public void FlipHorizontal_Works()
    {
        var rotator = new FrameRotator(flipHorizontal: true);
        var input = VideoFrame.Create(4, 2, VideoTypes.RGB24);
        // 第一行: BGR=(1,2,3) (4,5,6) (7,8,9) (10,11,12)
        for (var i = 0; i < input.Data.Length; i++)
            input.Data[i] = (Byte)(i + 1);

        var output = rotator.ProcessFrame(input);

        Assert.Equal(4, output.Width);
        Assert.Equal(2, output.Height);
        // 验证水平翻转：第一行最右像素应变为最左
        Assert.Equal(input.Data[9], output.Data[0]);  // B
        Assert.Equal(input.Data[10], output.Data[1]); // G
        Assert.Equal(input.Data[11], output.Data[2]); // R
    }

    [Fact(DisplayName = "FrameRotator在管线中正常工作")]
    public void InPipeline_Works()
    {
        var pipeline = new VideoPipeline()
            .Append(new FrameRotator(FrameRotator.RotationAngle.Clockwise90))
            .Append(new FrameResizer(32, 24));

        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(32, output.Width);
        Assert.Equal(24, output.Height);
    }
}
