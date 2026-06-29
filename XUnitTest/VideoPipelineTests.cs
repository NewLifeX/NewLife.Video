using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest;

public class VideoPipelineTests
{
    [Fact(DisplayName = "管线无处理器时原样返回帧")]
    public void EmptyPipeline_ReturnsSameFrame()
    {
        var pipeline = new VideoPipeline();
        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        // 填充测试数据
        for (var i = 0; i < input.Data.Length; i++)
            input.Data[i] = (Byte)(i % 256);

        var output = pipeline.ProcessFrame(input);

        // 数据应相同
        Assert.Equal(input.Data, output.Data);
        Assert.Equal(input.Width, output.Width);
        Assert.Equal(input.Height, output.Height);
    }

    [Fact(DisplayName = "管线单处理器正确传递帧")]
    public void SingleProcessor_ProcessesCorrectly()
    {
        var pipeline = new VideoPipeline();
        pipeline.Append(new TestProcessor());

        var input = VideoFrame.Create(32, 32, VideoTypes.RGB24);
        input.Timestamp = 100;

        var output = pipeline.ProcessFrame(input);

        // TestProcessor 将尺寸减半
        Assert.Equal(16, output.Width);
        Assert.Equal(16, output.Height);
        Assert.Equal(100, output.Timestamp);
    }

    [Fact(DisplayName = "管线多处理器链式处理")]
    public void MultipleProcessors_ChainsCorrectly()
    {
        var pipeline = new VideoPipeline();
        pipeline.Append(new TestProcessor()); // 32→16
        pipeline.Append(new TestProcessor()); // 16→8

        var input = VideoFrame.Create(32, 32, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(8, output.Width);
        Assert.Equal(8, output.Height);
    }

    [Fact(DisplayName = "管线流畅API链式调用")]
    public void FluentApi_WorksCorrectly()
    {
        var output = new VideoPipeline()
            .Append(new TestProcessor())
            .Append(new TestProcessor())
            .ProcessFrame(VideoFrame.Create(64, 64, VideoTypes.RGB24));

        Assert.Equal(16, output.Width);
    }

    [Fact(DisplayName = "管线Remove可移除处理器")]
    public void Remove_StopsProcessing()
    {
        var processor = new TestProcessor();
        var pipeline = new VideoPipeline();
        pipeline.Append(processor);
        pipeline.Remove(processor);

        Assert.Equal(0, pipeline.Count);

        var input = VideoFrame.Create(32, 32, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(32, output.Width); // 无处理器，尺寸不变
    }

    [Fact(DisplayName = "管线ProcessFrame传入null抛异常")]
    public void ProcessFrame_Null_Throws()
    {
        var pipeline = new VideoPipeline();
        Assert.Throws<ArgumentNullException>(() => pipeline.ProcessFrame(null));
    }

    [Fact(DisplayName = "管线Append传入null抛异常")]
    public void Append_Null_Throws()
    {
        var pipeline = new VideoPipeline();
        Assert.Throws<ArgumentNullException>(() => pipeline.Append(null));
    }

    /// <summary>测试用处理器：将帧尺寸减半</summary>
    private class TestProcessor : IVideoProcessor
    {
        public VideoTypes[] SupportedInputFormats => [VideoTypes.RGB24];
        public VideoTypes OutputFormat => VideoTypes.RGB24;

        public VideoFrame ProcessFrame(VideoFrame frame)
        {
            var half = VideoFrame.Create(frame.Width / 2, frame.Height / 2, VideoTypes.RGB24);
            half.Timestamp = frame.Timestamp;
            half.FrameIndex = frame.FrameIndex;
            return half;
        }
    }
}
