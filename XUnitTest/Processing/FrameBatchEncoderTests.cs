using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FrameBatchEncoderTests
{
    [Fact(DisplayName = "FrameBatchEncoder 空帧序列返回空")]
    public void EncodeBatch_EmptyInput_ReturnsEmpty()
    {
        var frames = Array.Empty<VideoFrame>();
        var result = FrameBatchEncoder.EncodeBatch(frames, BatchFormat.Jpeg, 80);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact(DisplayName = "FrameBatchEncoder 单帧序列")]
    public void EncodeBatch_SingleFrame_ReturnsSingleOutput()
    {
        var frame = VideoFrame.Create(4, 4, VideoTypes.RGB24);
        var frames = new[] { frame };
        var result = FrameBatchEncoder.EncodeBatch(frames, BatchFormat.Jpeg, 80);
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact(DisplayName = "FrameBatchEncoder 多帧序列")]
    public void EncodeBatch_MultipleFrames_ReturnsCorrectCount()
    {
        var frames = new[]
        {
            VideoFrame.Create(4, 4, VideoTypes.RGB24),
            VideoFrame.Create(4, 4, VideoTypes.RGB24),
            VideoFrame.Create(4, 4, VideoTypes.RGB24),
        };
        var result = FrameBatchEncoder.EncodeBatch(frames, BatchFormat.Jpeg, 80);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
    }

    [Fact(DisplayName = "FrameBatchEncoder 不支持格式回退 Jpeg")]
    public void EncodeBatch_UnsupportedFormat_FallsBackToJpeg()
    {
        var frame = VideoFrame.Create(4, 4, VideoTypes.RGB24);
        var frames = new[] { frame };
        var result = FrameBatchEncoder.EncodeBatch(frames, (BatchFormat)99, 80);
        Assert.NotNull(result);
        // 不支持格式可能会回退到默认格式（Jpeg），至少返回 1 个结果
        Assert.NotEmpty(result);
    }
}
