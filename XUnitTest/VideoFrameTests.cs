using NewLife.Video;
using Xunit;

namespace XUnitTest;

public class VideoFrameTests
{
    [Fact(DisplayName = "VideoFrame.Create创建正确尺寸的YUV420P帧")]
    public void Create_YUV420P_CorrectSize()
    {
        var frame = VideoFrame.Create(1920, 1080, VideoTypes.YUV420P);

        Assert.Equal(1920, frame.Width);
        Assert.Equal(1080, frame.Height);
        Assert.Equal(VideoTypes.YUV420P, frame.PixelFormat);
        Assert.NotNull(frame.Data);

        // YUV420P: Y(1920*1080) + U(960*540) + V(960*540) = 3110400
        var expectedSize = 1920 * 1080 + 2 * (960 * 540);
        Assert.Equal(expectedSize, frame.Data.Length);
    }

    [Fact(DisplayName = "VideoFrame.Create创建正确尺寸的NV12帧")]
    public void Create_NV12_CorrectSize()
    {
        var frame = VideoFrame.Create(640, 480, VideoTypes.NV12);

        Assert.Equal(640, frame.Width);
        Assert.Equal(480, frame.Height);
        Assert.Equal(VideoTypes.NV12, frame.PixelFormat);

        // NV12: Y(640*480) + UV(640*480/2) = 460800
        var expectedSize = 640 * 480 + 640 * 480 / 2;
        Assert.Equal(expectedSize, frame.Data.Length);
    }

    [Fact(DisplayName = "VideoFrame.Create创建正确尺寸的RGB24帧")]
    public void Create_RGB24_CorrectSize()
    {
        var frame = VideoFrame.Create(800, 600, VideoTypes.RGB24);

        // RGB24: 800*600*3 = 1440000
        var expectedSize = 800 * 600 * 3;
        Assert.Equal(expectedSize, frame.Data.Length);
    }

    [Fact(DisplayName = "VideoFrame.CalculateDataSize计算各格式字节数")]
    public void CalculateDataSize_AllFormats()
    {
        Assert.Equal(640 * 480 * 3, VideoFrame.CalculateDataSize(640, 480, VideoTypes.RGB24));
        Assert.Equal(640 * 480 * 3, VideoFrame.CalculateDataSize(640, 480, VideoTypes.BGR24));
        Assert.Equal(640 * 480 * 4, VideoFrame.CalculateDataSize(640, 480, VideoTypes.BGRA32));
        Assert.Equal(640 * 480 * 4, VideoFrame.CalculateDataSize(640, 480, VideoTypes.RGBA32));
        Assert.Equal(640 * 480, VideoFrame.CalculateDataSize(640, 480, VideoTypes.GRAY8));

        // YUV420P: Y(全分辨率) + U(半分辨率) + V(半分辨率)
        var yuv420p = 640 * 480 + 2 * (320 * 240);
        Assert.Equal(yuv420p, VideoFrame.CalculateDataSize(640, 480, VideoTypes.YUV420P));
    }

    [Fact(DisplayName = "VideoFrame平面偏移在Create时正确设置")]
    public void Create_YUV420P_PlaneOffsetsCorrect()
    {
        var frame = VideoFrame.Create(640, 480, VideoTypes.YUV420P);

        Assert.Equal(0, frame.YOffset);
        Assert.Equal(640 * 480, frame.UOffset);  // Y 平面大小
        Assert.Equal(640 * 480 + 320 * 240, frame.VOffset); // Y+U 平面大小
        Assert.Equal(640, frame.YStride);
        Assert.Equal(320, frame.UVStride);
    }

    [Fact(DisplayName = "GetYPlane返回正确数据")]
    public void GetYPlane_ReturnsCorrectSpan()
    {
        var frame = VideoFrame.Create(320, 240, VideoTypes.YUV420P);
        // 填充一些测试数据
        for (var i = 0; i < frame.Width * frame.Height; i++)
            frame.Data[i] = (Byte)(i % 256);

        var ySpan = frame.GetYPlane();
        Assert.Equal(frame.Width * frame.Height, ySpan.Length);
        Assert.Equal(0, ySpan[0]);
        Assert.Equal(1, ySpan[1]);
    }

    [Fact(DisplayName = "GetUPlane和GetVPlane返回正确数据")]
    public void GetUPlane_GetVPlane_ReturnsCorrectSpan()
    {
        var frame = VideoFrame.Create(320, 240, VideoTypes.YUV420P);
        var chromaSize = 160 * 120; // 320/2 * 240/2

        var uSpan = frame.GetUPlane();
        var vSpan = frame.GetVPlane();

        Assert.Equal(chromaSize, uSpan.Length);
        Assert.Equal(chromaSize, vSpan.Length);
    }
}
