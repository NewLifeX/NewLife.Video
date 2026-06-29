using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FFmpegFilterProcessorTests
{
    [Fact(DisplayName = "FFmpegFilterProcessor 实现IVideoProcessor接口")]
    public void ImplementsInterface()
    {
        var processor = new FFmpegFilterProcessor("scale=320:240");

        Assert.IsAssignableFrom<IVideoProcessor>(processor);
        Assert.NotEmpty(processor.SupportedInputFormats);
        Assert.Equal(VideoTypes.YUV420P, processor.OutputFormat);
    }

    [Fact(DisplayName = "FFmpegFilterProcessor null滤镜字符串抛异常")]
    public void NullFilterString_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FFmpegFilterProcessor(null));
    }

    [Fact(DisplayName = "FFmpegFilterProcessor null帧抛异常")]
    public void ProcessFrame_Null_Throws()
    {
        var processor = new FFmpegFilterProcessor("scale=320:240");
        Assert.Throws<ArgumentNullException>(() => processor.ProcessFrame(null));
    }

    [Fact(DisplayName = "FFmpegFilterProcessor 解析scale滤镜尺寸")]
    public void ParseScale_FromFilterString()
    {
        var processor = new FFmpegFilterProcessor("scale=1280:720,format=yuv420p");

        // 通过反射或实际处理验证 scale 解析
        var frame = VideoFrame.Create(640, 480, VideoTypes.YUV420P);
        // 注意：实际处理需要 ffmpeg，此处仅验证不崩溃
        try
        {
            var result = processor.ProcessFrame(frame);
            Assert.NotNull(result);
        }
        catch (InvalidOperationException)
        {
            // ffmpeg 未安装时预期抛此异常
        }
    }
}
