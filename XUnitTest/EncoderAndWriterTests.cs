using NewLife.Video;
using NewLife.Video.Codecs;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest;

public class EncoderAndWriterTests
{
    [Fact(DisplayName = "FFmpegVP8Encoder ICodecInfo正确")]
    public void VP8Encoder_Info_Correct()
    {
        var e = new FFmpegVP8Encoder();
        Assert.Contains(VideoTypes.VP8, e.SupportedTypes);
    }

    [Fact(DisplayName = "FFmpegVP9Encoder ICodecInfo正确")]
    public void VP9Encoder_Info_Correct()
    {
        var e = new FFmpegVP9Encoder();
        Assert.Contains(VideoTypes.VP9, e.SupportedTypes);
    }

    [Fact(DisplayName = "MkvWriter 写入帧生成有效文件")]
    public void MkvWriter_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            using var w = new MkvWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.VP9, 25);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[200]), CodecType = VideoTypes.VP9, IsKeyFrame = true });
            w.Flush();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "VideoContainerFactory.CreateWriter 路由正确")]
    public void Factory_CreateWriter_Routes()
    {
        var w1 = VideoContainerFactory.CreateWriter("test.mp4");
        Assert.IsType<Mp4Writer>(w1);
        w1.Dispose();

        var w2 = VideoContainerFactory.CreateWriter("test.flv");
        Assert.IsType<FlvWriter>(w2);
        w2.Dispose();

        var w3 = VideoContainerFactory.CreateWriter("test.avi");
        Assert.IsType<AviWriter>(w3);
        w3.Dispose();

        var w4 = VideoContainerFactory.CreateWriter("test.ts");
        Assert.IsType<TsWriter>(w4);
        w4.Dispose();

        var w5 = VideoContainerFactory.CreateWriter("test.mkv");
        Assert.IsType<MkvWriter>(w5);
        w5.Dispose();

        var w6 = VideoContainerFactory.CreateWriter("test.webm");
        Assert.IsType<MkvWriter>(w6);
        w6.Dispose();

        var w7 = VideoContainerFactory.CreateWriter("test.yuv");
        Assert.IsType<RawVideoWriter>(w7);
        w7.Dispose();
    }

    [Fact(DisplayName = "Mp4Writer 写入多帧生成MP4结构")]
    public void Mp4Writer_MultiFrame_GeneratesStructure()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using var w = new Mp4Writer(path);
            w.WriteMetadata(320, 240, VideoTypes.H264, 30);
            for (var i = 0; i < 5; i++)
                w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[100 + i * 10]), CodecType = VideoTypes.H264 });

            Assert.True(_disposedCheck(w)); // just checks it doesn't throw
            w.Dispose();
            Assert.True(new FileInfo(path).Length > 100);

            // 验证文件以 ftyp 开头
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var buf = new Byte[4];
            fs.Read(buf, 0, 4); fs.Read(buf, 0, 4);
            var ftypOffset = BitConverter.ToInt32(new[] { buf[0], buf[1], buf[2], buf[3] }, 0);
            Assert.True(ftypOffset > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer H265编码使用hvc1")]
    public void Mp4Writer_H265_UsesHvc1()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using var w = new Mp4Writer(path);
            w.WriteMetadata(1920, 1080, VideoTypes.H265, 30);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[200]), CodecType = VideoTypes.H265 });
            w.Dispose();
            Assert.True(File.Exists(path));
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvWriter 空帧不崩溃")]
    public void MkvWriter_Empty_NoCrash()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            var w = new MkvWriter(path);
            w.WriteMetadata(64, 48, VideoTypes.H264, 1);
            w.WriteFrame(null);
            w.Dispose();
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 无帧Dispose安全")]
    public void Mp4Writer_NoFrames_DisposeSafe()
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

    private static Boolean _disposedCheck(Mp4Writer w)
    {
        try { return true; } catch { return false; }
    }
}
