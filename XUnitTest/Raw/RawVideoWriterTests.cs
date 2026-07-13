using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.Raw;
using Xunit;

namespace XUnitTest.Raw;

public class RawVideoWriterTests
{
    [Fact(DisplayName = "RawVideoWriter 写入YUV帧正确")]
    public void WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            using var w = new RawVideoWriter(path);
            w.WriteMetadata(320, 240, VideoTypes.YUV420P, 25);
            var frameSize = VideoFrame.CalculateDataSize(320, 240, VideoTypes.YUV420P);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[frameSize]), CodecType = VideoTypes.YUV420P });
            w.Flush();

            Assert.Equal(frameSize, new FileInfo(path).Length);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "RawVideoWriter 工厂路由.yuv扩展名")]
    public void Factory_RoutesYuv()
    {
        var w = VideoContainerFactory.CreateWriter("test.yuv");
        Assert.IsType<RawVideoWriter>(w);
        w.Dispose();
    }
}
