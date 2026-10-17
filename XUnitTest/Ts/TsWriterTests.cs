using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.Ts;
using Xunit;

namespace XUnitTest.Ts;

public class TsWriterTests
{
    [Fact(DisplayName = "TsWriter 写入帧正确")]
    public void WriteFrame_Works()
    {
        var path = TestFile.GetTempPath(".ts");
        try
        {
            using var w = new TsWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[500]), CodecType = VideoTypes.H264 });
            w.Flush();

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "TsWriter 工厂路由.ts扩展名")]
    public void Factory_RoutesTs()
    {
        var w = VideoContainerFactory.CreateWriter("test.ts");
        Assert.IsType<TsWriter>(w);
        w.Dispose();
    }
}
