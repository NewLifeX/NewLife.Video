using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class VideoTranscoderTests
{
    [Fact(DisplayName = "VideoTranscoder 空路径抛出异常")]
    public void Transcode_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VideoTranscoder.Transcode(null, "out.mp4"));
        Assert.Throws<ArgumentNullException>(() => VideoTranscoder.Transcode("in.mp4", null));
    }

    [Fact(DisplayName = "VideoTranscoder 空字符串路径抛出异常")]
    public void Transcode_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VideoTranscoder.Transcode("", "out.mp4"));
        Assert.Throws<ArgumentNullException>(() => VideoTranscoder.Transcode("in.mp4", ""));
    }

    [Fact(DisplayName = "VideoTranscoder 不存在的输入文件抛出异常")]
    public void Transcode_NonExistentInput_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => VideoTranscoder.Transcode("nonexistent.mp4", "out.mp4"));
    }

    [Fact(DisplayName = "VideoTranscoder 默认选项不为空")]
    public void Transcode_DefaultOptions_NotNull()
    {
        var options = new TranscodeOptions();
        Assert.Null(options.EncoderName);
        Assert.Equal(0, options.Width);
        Assert.Equal(0, options.Height);
        Assert.Equal(0, options.FrameRate);
        Assert.Equal(0, options.Bitrate);
    }

    [Fact(DisplayName = "TranscodeOptions 属性设置")]
    public void TranscodeOptions_Properties()
    {
        var options = new TranscodeOptions
        {
            EncoderName = "libx264",
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            Bitrate = 5000000,
        };
        Assert.Equal("libx264", options.EncoderName);
        Assert.Equal(1920, options.Width);
        Assert.Equal(1080, options.Height);
        Assert.Equal(30, options.FrameRate);
        Assert.Equal(5000000, options.Bitrate);
    }
}
