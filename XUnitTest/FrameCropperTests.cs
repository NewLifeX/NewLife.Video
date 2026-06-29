using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest;

public class FrameCropperTests
{
    [Fact(DisplayName = "FrameCropper裁剪尺寸正确")]
    public void Crop_CorrectDimensions()
    {
        var cropper = new FrameCropper(10, 10, 32, 24);
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);

        var output = cropper.ProcessFrame(input);

        Assert.Equal(32, output.Width);
        Assert.Equal(24, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "FrameCropper从(0,0)裁剪等同缩小")]
    public void Crop_FromOrigin_CorrectSize()
    {
        var cropper = new FrameCropper(0, 0, 16, 16);
        var input = VideoFrame.Create(32, 32, VideoTypes.RGB24);

        var output = cropper.ProcessFrame(input);

        Assert.Equal(16, output.Width);
        Assert.Equal(16, output.Height);
    }

    [Fact(DisplayName = "FrameCropper保持元数据")]
    public void Crop_PreservesMetadata()
    {
        var cropper = new FrameCropper(0, 0, 8, 8);
        var input = VideoFrame.Create(16, 16, VideoTypes.RGB24);
        input.Timestamp = 5678;
        input.FrameIndex = 7;

        var output = cropper.ProcessFrame(input);

        Assert.Equal(5678, output.Timestamp);
        Assert.Equal(7, output.FrameIndex);
    }
}
