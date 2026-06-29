using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class TimestampOverlayTests
{
    [Fact(DisplayName = "TimestampOverlay默认格式叠加时间")]
    public void Overlay_DefaultFormat_Works()
    {
        var overlay = new TimestampOverlay();
        var input = VideoFrame.Create(128, 64, VideoTypes.RGB24);
        input.Timestamp = 1000;

        var output = overlay.ProcessFrame(input);

        Assert.NotNull(output);
        Assert.Equal(128, output.Width);
        Assert.Equal(64, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "TimestampOverlay自定义格式")]
    public void Overlay_CustomFormat_Works()
    {
        var overlay = new TimestampOverlay("HH:mm:ss.fff", x: 20, y: 30, r: 0, g: 255, b: 0);
        var input = VideoFrame.Create(128, 64, VideoTypes.RGB24);

        var output = overlay.ProcessFrame(input);

        Assert.NotNull(output);
    }

    [Fact(DisplayName = "TimestampOverlay基准时间模式")]
    public void Overlay_BaseTime_Works()
    {
        var overlay = new TimestampOverlay("HH:mm:ss");
        overlay.SetBaseTime(new DateTime(2026, 6, 29, 10, 0, 0));

        var input = VideoFrame.Create(128, 64, VideoTypes.RGB24);
        input.Timestamp = 5000; // 5 秒后

        var output = overlay.ProcessFrame(input);

        Assert.NotNull(output);
        // 输出时间应为 10:00:05
    }

    [Fact(DisplayName = "TimestampOverlay在管线中正常工作")]
    public void InPipeline_Works()
    {
        var pipeline = new VideoPipeline()
            .Append(new TimestampOverlay("HH:mm:ss"))
            .Append(new FrameResizer(64, 32));

        var input = VideoFrame.Create(128, 64, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(64, output.Width);
        Assert.Equal(32, output.Height);
    }
}
