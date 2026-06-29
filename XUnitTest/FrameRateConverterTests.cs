using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest;

public class FrameRateConverterTests
{
    [Fact(DisplayName = "FrameRateConverter 30→15降帧率正确")]
    public void Convert_30to15_SkipsEveryOther()
    {
        var conv = new FrameRateConverter(30, 15);
        var frames = new List<VideoFrame>();

        for (var i = 0; i < 10; i++)
        {
            var frame = VideoFrame.Create(64, 48, VideoTypes.RGB24);
            frame.FrameIndex = i;
            frames.AddRange(conv.ProcessFrame(frame));
        }

        // 30→15 应该输出约 5 帧 (每 2 帧输出 1 帧)
        Assert.True(frames.Count >= 4 && frames.Count <= 6, $"Expected ~5, got {frames.Count}");
    }

    [Fact(DisplayName = "FrameRateConverter 15→30升帧率正确")]
    public void Convert_15to30_DuplicatesFrames()
    {
        var conv = new FrameRateConverter(15, 30);
        var frames = new List<VideoFrame>();

        for (var i = 0; i < 5; i++)
        {
            frames.AddRange(conv.ProcessFrame(VideoFrame.Create(64, 48, VideoTypes.RGB24)));
        }

        // 15→30 应该输出约 10 帧
        Assert.True(frames.Count >= 9 && frames.Count <= 11, $"Expected ~10, got {frames.Count}");
    }

    [Fact(DisplayName = "FrameRateConverter 同帧率不变")]
    public void Convert_SameFps_PassThrough()
    {
        var conv = new FrameRateConverter(25, 25);
        var frames = new List<VideoFrame>();

        for (var i = 0; i < 10; i++)
        {
            var frame = VideoFrame.Create(64, 48, VideoTypes.RGB24);
            frame.FrameIndex = i;
            frames.AddRange(conv.ProcessFrame(frame));
        }

        Assert.True(frames.Count >= 9 && frames.Count <= 11, $"Expected ~10, got {frames.Count}");
    }

    [Fact(DisplayName = "FrameRateConverter Reset重置状态")]
    public void Reset_ClearsState()
    {
        var conv = new FrameRateConverter(30, 15);
        conv.ProcessFrame(VideoFrame.Create(64, 48, VideoTypes.RGB24)).ToList();
        conv.ProcessFrame(VideoFrame.Create(64, 48, VideoTypes.RGB24)).ToList();
        conv.Reset();

        // Reset 后重新开始，行为应与新实例一致
        var frames = new List<VideoFrame>();
        for (var i = 0; i < 10; i++)
            frames.AddRange(conv.ProcessFrame(VideoFrame.Create(64, 48, VideoTypes.RGB24)));
        Assert.True(frames.Count >= 4, $"Expected >=4, got {frames.Count}");
    }

    [Fact(DisplayName = "FrameRateConverter 无效参数抛异常")]
    public void Constructor_Invalid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateConverter(0, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateConverter(30, 0));
    }
}
