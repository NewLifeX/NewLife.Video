using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.Mkv;
using Xunit;

namespace XUnitTest.Mkv;

public class MkvWriterTests
{
    [Fact(DisplayName = "MkvWriter 写入帧生成有效文件")]
    public void WriteFrame_Works()
    {
        var path = TestFile.GetTempPath(".mkv");
        try
        {
            using var w = new MkvWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.VP9, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[200]), CodecType = VideoTypes.VP9, IsKeyFrame = true });
            w.Flush();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvWriter 空帧不崩溃")]
    public void Empty_NoCrash()
    {
        var path = TestFile.GetTempPath(".mkv");
        try
        {
            var w = new MkvWriter(path);
            w.WriteMetadata(64, 48, VideoTypes.H264, 1);
            w.WriteFrame(null);
            w.Dispose();
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvWriter 工厂路由.mkv和.webm")]
    public void Factory_RoutesBoth()
    {
        var w1 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".mkv"));
        Assert.IsType<MkvWriter>(w1);
        w1.Dispose();

        var w2 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".webm"));
        Assert.IsType<MkvWriter>(w2);
        w2.Dispose();
    }
}
