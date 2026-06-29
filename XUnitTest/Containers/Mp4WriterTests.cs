using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest.Containers;

public class Mp4WriterTests
{
    [Fact(DisplayName = "Mp4Writer 写入帧正确")]
    public void WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using (var w = new Mp4Writer(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.H264, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[300]), CodecType = VideoTypes.H264 });
            }

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 写入多帧生成MP4结构")]
    public void MultiFrame_GeneratesStructure()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            var w = new Mp4Writer(path);
            w.WriteMetadata(320, 240, VideoTypes.H264, 30);
            for (var i = 0; i < 5; i++)
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[100 + i * 10]), CodecType = VideoTypes.H264 });
            w.Dispose();

            Assert.True(new FileInfo(path).Length > 100);

            // 验证文件以 ftyp 开头
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var buf = new Byte[4];
            fs.Read(buf, 0, 4);
            var ftypOffset = BitConverter.ToInt32(new[] { buf[0], buf[1], buf[2], buf[3] }, 0);
            Assert.True(ftypOffset > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer H265编码使用hvc1")]
    public void H265_UsesHvc1()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using var w = new Mp4Writer(path);
            w.WriteMetadata(1920, 1080, VideoTypes.H265, 30);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[200]), CodecType = VideoTypes.H265 });
            w.Dispose();
            Assert.True(File.Exists(path));
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 无帧Dispose安全")]
    public void NoFrames_DisposeSafe()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using var w = new Mp4Writer(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.Dispose();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 工厂路由.mp4扩展名")]
    public void Factory_RoutesMp4()
    {
        var w = VideoContainerFactory.CreateWriter("test.mp4");
        Assert.IsType<Mp4Writer>(w);
        w.Dispose();
    }
}
