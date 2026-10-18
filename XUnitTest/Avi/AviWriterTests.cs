using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Avi;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest.Avi;

public class AviWriterTests
{
    [Fact(DisplayName = "AviWriter 写入帧正确")]
    public void WriteFrame_Works()
    {
        var path = TestFile.GetTempPath(".avi");
        try
        {
            using (var w = new AviWriter(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.MJPEG, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[150]), CodecType = VideoTypes.MJPEG });
            }

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter 生成标准AVI文件头部")]
    public void GeneratesValidHeader()
    {
        var path = TestFile.GetTempPath(".avi");
        try
        {
            using (var w = new AviWriter(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.MJPEG, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[100]), CodecType = VideoTypes.MJPEG });
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[200]), CodecType = VideoTypes.MJPEG });
            }

            var data = File.ReadAllBytes(path);
            Assert.True(data.Length > 200);

            // 验证 RIFF 头
            var riff = System.Text.Encoding.ASCII.GetString(data, 0, 4);
            Assert.Equal("RIFF", riff);
            var aviType = System.Text.Encoding.ASCII.GetString(data, 8, 4);
            Assert.Equal("AVI ", aviType);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter H264编码使用H264 FourCC")]
    public void H264_UsesH264FourCC()
    {
        var path = TestFile.GetTempPath(".avi");
        try
        {
            using (var w = new AviWriter(path))
            {
                w.WriteMetadata(640, 480, VideoTypes.H264, 30);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[50]), CodecType = VideoTypes.H264 });
            }

            var data = File.ReadAllBytes(path);
            // 在 strf 中查找 "H264"
            var found = false;
            for (var i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == 'H' && data[i + 1] == '2' && data[i + 2] == '6' && data[i + 3] == '4')
                    found = true;
            }
            Assert.True(found, "AVI 文件应包含 H264 FourCC");
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter 包含idx1索引")]
    public void IncludesIdx1()
    {
        var path = TestFile.GetTempPath(".avi");
        try
        {
            using (var w = new AviWriter(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.MJPEG, 10);
                for (var i = 0; i < 3; i++)
                    w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[80 + i * 10]), CodecType = VideoTypes.MJPEG });
            }

            var data = File.ReadAllBytes(path);
            var idx1Pos = -1;
            for (var i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == 'i' && data[i + 1] == 'd' && data[i + 2] == 'x' && data[i + 3] == '1')
                    idx1Pos = i;
            }
            Assert.True(idx1Pos > 0, "AVI 文件应包含 idx1 索引块");
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter 可通过AviReader回读")]
    public void RoundTrip_WithReader()
    {
        var path = TestFile.GetTempPath(".avi");
        try
        {
            using (var w = new AviWriter(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.MJPEG, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[150]), CodecType = VideoTypes.MJPEG });
            }

            using var reader = new AviReader(path);
            Assert.Equal(VideoTypes.MJPEG, reader.VideoCodecType);
            Assert.Equal(320, reader.VideoWidth);
            Assert.Equal(240, reader.VideoHeight);
            Assert.True(reader.TotalFrames >= 1);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter 工厂路由.avi扩展名")]
    public void Factory_RoutesAvi()
    {
        var w = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".avi"));
        Assert.IsType<AviWriter>(w);
        w.Dispose();
    }
}
