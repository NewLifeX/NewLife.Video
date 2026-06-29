using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest.Containers;

public class FlvWriterTests
{
    [Fact(DisplayName = "FlvWriter 写入帧正确")]
    public void WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            using var w = new FlvWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[200]), CodecType = VideoTypes.H264, IsKeyFrame = true });
            w.Flush();

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "FlvWriter 工厂路由.flv扩展名")]
    public void Factory_RoutesFlv()
    {
        var w = VideoContainerFactory.CreateWriter("test.flv");
        Assert.IsType<FlvWriter>(w);
        w.Dispose();
    }
}
