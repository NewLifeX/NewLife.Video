using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest;

public class WatermarkOverlayTests
{
    [Fact(DisplayName = "WatermarkOverlay叠加文字后尺寸不变")]
    public void Overlay_SameDimensions()
    {
        var overlay = new WatermarkOverlay("TEST");
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        Array.Fill(input.Data, (Byte)128); // 灰色背景

        var output = overlay.ProcessFrame(input);

        Assert.Equal(64, output.Width);
        Assert.Equal(48, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "WatermarkOverlay多行文字")]
    public void Overlay_MultiLine_Works()
    {
        var overlay = new WatermarkOverlay("LINE1\nLINE2");
        var input = VideoFrame.Create(128, 96, VideoTypes.RGB24);
        Array.Fill(input.Data, (Byte)0); // 黑色背景

        var output = overlay.ProcessFrame(input);

        // 不抛异常即通过
        Assert.NotNull(output);
    }

    [Fact(DisplayName = "WatermarkOverlay保持时间戳")]
    public void Overlay_PreservesTimestamp()
    {
        var overlay = new WatermarkOverlay("TS");
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        input.Timestamp = 9999;
        input.FrameIndex = 42;

        var output = overlay.ProcessFrame(input);

        Assert.Equal(9999, output.Timestamp);
        Assert.Equal(42, output.FrameIndex);
    }

    [Fact(DisplayName = "WatermarkOverlay自定义颜色")]
    public void Overlay_CustomColor_Works()
    {
        var overlay = new WatermarkOverlay("RED", r: 255, g: 0, b: 0);
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        Array.Fill(input.Data, (Byte)0);

        var output = overlay.ProcessFrame(input);

        Assert.NotNull(output);
        Assert.Equal(64, output.Width);
    }

    [Fact(DisplayName = "WatermarkOverlay透明度参数")]
    public void Overlay_Opacity_Clamp()
    {
        // 透明度 0.0~1.0 均合法
        var overlay1 = new WatermarkOverlay("T1", opacity: 0.3);
        var overlay2 = new WatermarkOverlay("T2", opacity: 1.5); // 应钳位到 1.0

        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);

        var output1 = overlay1.ProcessFrame(input);
        var output2 = overlay2.ProcessFrame(input);

        Assert.NotNull(output1);
        Assert.NotNull(output2);
    }

    [Fact(DisplayName = "WatermarkOverlay空文本抛异常")]
    public void Overlay_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WatermarkOverlay(null));
    }

    [Fact(DisplayName = "WatermarkOverlay在管线中正常工作")]
    public void InPipeline_Works()
    {
        var pipeline = new VideoPipeline()
            .Append(new FrameResizer(64, 48))
            .Append(new WatermarkOverlay("PIPELINE"));

        var input = VideoFrame.Create(128, 96, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(64, output.Width);
        Assert.Equal(48, output.Height);
    }
}
