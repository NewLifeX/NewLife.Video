using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class MotionDetectorTests
{
    [Fact(DisplayName = "MotionDetector首帧无参考返回空")]
    public void FirstFrame_NoReference_ReturnsEmpty()
    {
        var detector = new MotionDetector();
        var frame = VideoFrame.Create(64, 48, VideoTypes.YUV420P);

        var regions = detector.Detect(frame);

        Assert.Empty(regions);
    }

    [Fact(DisplayName = "MotionDetector相同帧无运动")]
    public void SameFrame_NoMotion()
    {
        var detector = new MotionDetector { Threshold = 10 };
        var frame1 = VideoFrame.Create(32, 32, VideoTypes.YUV420P);
        var frame2 = VideoFrame.Create(32, 32, VideoTypes.YUV420P);

        // 两帧完全相同
        Array.Fill(frame1.Data, (Byte)128);
        Array.Fill(frame2.Data, (Byte)128);

        detector.Detect(frame1); // 设置参考帧
        var regions = detector.Detect(frame2);

        Assert.Empty(regions);
    }

    [Fact(DisplayName = "MotionDetector检测到运动区域")]
    public void DifferentFrames_DetectsMotion()
    {
        var detector = new MotionDetector { Threshold = 20, MinArea = 10 };
        var width = 32;
        var height = 32;

        var frame1 = VideoFrame.Create(width, height, VideoTypes.YUV420P);
        var frame2 = VideoFrame.Create(width, height, VideoTypes.YUV420P);

        // 帧1: 全黑 Y=16
        Array.Fill(frame1.Data, (Byte)16);
        // 帧2: 中间 8x8 区域变亮 Y=200
        Array.Fill(frame2.Data, (Byte)16);
        for (var y = 12; y < 20; y++)
            for (var x = 12; x < 20; x++)
                frame2.Data[y * width + x] = 200;

        detector.Detect(frame1);
        var regions = detector.Detect(frame2);

        Assert.NotEmpty(regions);
        Assert.True(regions[0].Area >= 10, $"Area={regions[0].Area}");
    }

    [Fact(DisplayName = "MotionDetector RGB帧也能检测")]
    public void RGBFrame_DetectsMotion()
    {
        var detector = new MotionDetector { Threshold = 30, MinArea = 5 };
        var width = 16;
        var height = 16;

        var frame1 = VideoFrame.Create(width, height, VideoTypes.RGB24);
        var frame2 = VideoFrame.Create(width, height, VideoTypes.RGB24);

        // 帧1: 灰色 RGB(128,128,128)
        for (var i = 0; i < frame1.Data.Length; i++)
            frame1.Data[i] = 128;
        // 帧2: 白色 RGB(255,255,255)
        for (var i = 0; i < frame2.Data.Length; i++)
            frame2.Data[i] = 255;

        detector.Detect(frame1);
        var regions = detector.Detect(frame2);

        Assert.NotEmpty(regions);
    }

    [Fact(DisplayName = "MotionDetector Reset清除参考帧")]
    public void Reset_ClearsReference()
    {
        var detector = new MotionDetector();
        var frame = VideoFrame.Create(16, 16, VideoTypes.YUV420P);

        detector.Detect(frame);
        detector.Reset();

        var regions = detector.Detect(frame);
        Assert.Empty(regions); // Reset 后第一帧又是参考帧
    }

    [Fact(DisplayName = "MotionDetector阈值参数生效")]
    public void Threshold_HighValue_NoDetection()
    {
        var detector = new MotionDetector { Threshold = 200, MinArea = 1 };
        var frame1 = VideoFrame.Create(16, 16, VideoTypes.YUV420P);
        var frame2 = VideoFrame.Create(16, 16, VideoTypes.YUV420P);

        Array.Fill(frame1.Data, (Byte)16);
        Array.Fill(frame2.Data, (Byte)200); // 变化 184，小于阈值 200

        detector.Detect(frame1);
        var regions = detector.Detect(frame2);

        Assert.Empty(regions);
    }

    [Fact(DisplayName = "MotionDetector最小区域过滤")]
    public void MinArea_FiltersTinyRegions()
    {
        var detector = new MotionDetector { Threshold = 10, MinArea = 10000 };
        var frame1 = VideoFrame.Create(32, 32, VideoTypes.YUV420P);
        var frame2 = VideoFrame.Create(32, 32, VideoTypes.YUV420P);

        Array.Fill(frame1.Data, (Byte)16);
        Array.Fill(frame2.Data, (Byte)255);

        detector.Detect(frame1);
        var regions = detector.Detect(frame2);

        // 整个帧都在变化，但 MinArea 要求很大，应被过滤
        // 实际上整个帧的面积是 1024，小于 10000
        Assert.Empty(regions);
    }
}
