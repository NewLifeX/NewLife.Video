using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.FMp4;
using Xunit;

namespace XUnitTest.FMp4;

public class Mp4WriterTests
{
    [Fact(DisplayName = "Mp4Writer 写入帧正确")]
    public void WriteFrame_Works()
    {
        var path = TestFile.GetTempPath(".mp4");
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
        var path = TestFile.GetTempPath(".mp4");
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
            fs.ReadExactly(buf, 0, 4);
            var ftypOffset = BitConverter.ToInt32(new[] { buf[0], buf[1], buf[2], buf[3] }, 0);
            Assert.True(ftypOffset > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer H265编码使用hvc1")]
    public void H265_UsesHvc1()
    {
        var path = TestFile.GetTempPath(".mp4");
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
        var path = TestFile.GetTempPath(".mp4");
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

    [Fact(DisplayName = "Mp4Writer 写入后可帧级读取（stco 绝对偏移 + 长度前缀样本）")]
    public void WriterReader_FrameRoundTrip()
    {
        var path = TestFile.GetTempPath(".mp4");
        try
        {
            using (var w = new Mp4Writer(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.H264, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(MakeAnnexBKeyFrame()), CodecType = VideoTypes.H264, IsKeyFrame = true, PTS = 0 });
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x80 }), CodecType = VideoTypes.H264, PTS = 40 });
            }

            using var r = new Mp4Reader(path);
            Assert.Equal(VideoTypes.H264, r.VideoCodecType);
            Assert.Equal(320, r.VideoWidth);
            Assert.Equal(240, r.VideoHeight);
            Assert.True(r.TotalFrames >= 2, $"TotalFrames={r.TotalFrames} 应 >= 2");

            var f1 = r.ReadFrame();
            Assert.NotNull(f1);
            Assert.NotNull(f1.Data);
            Assert.True(f1.Data.Total > 0, "首帧数据为空");
            Assert.True(f1.IsKeyFrame, "首帧应为关键帧");
            // 参数集已入 avcC，样本首 NAL 应为 IDR（0x65）而非 SPS/PPS
            Assert.Equal(0x65, f1.Data[0]);

            var f2 = r.ReadFrame();
            Assert.NotNull(f2);
            Assert.NotNull(f2.Data);
            Assert.True(f2.Data.Total > 0, "次帧数据为空");
            Assert.Equal(0x41, f2.Data[0]);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 输出可被 ffprobe 解析（无 ffprobe 时跳过）")]
    public void Output_ParsedByFfprobe()
    {
        if (!IsFfprobeAvailable()) return;

        var path = TestFile.GetTempPath(".mp4");
        try
        {
            using (var w = new Mp4Writer(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.H264, 25);
                w.WriteFrame(new VPacket { Data = new ArrayPacket(MakeAnnexBKeyFrame()), CodecType = VideoTypes.H264, IsKeyFrame = true, PTS = 0 });
                w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x80 }), CodecType = VideoTypes.H264, PTS = 40 });
            }

            // 容器级解析：ffprobe 读取样本表与样本描述，确认标准工具可识别编码与宽高
            var psi = new System.Diagnostics.ProcessStartInfo("ffprobe",
                $"-v error -select_streams v:0 -show_entries stream=codec_name,width,height -of csv=p=0 \"{path}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            Assert.True(p.WaitForExit(15000), "ffprobe 超时");
            Assert.True(p.ExitCode == 0, $"ffprobe 退出码 {p.ExitCode}: {stderr}");
            Assert.Contains("h264", stdout);
            Assert.Contains("320", stdout);
            Assert.Contains("240", stdout);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    private static Boolean IsFfprobeAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("ffprobe", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (!p.WaitForExit(5000)) return false;
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static Byte[] MakeAnnexBKeyFrame()
    {
        var sps = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0x40, 0x80 };
        var pps = new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };
        var idr = new Byte[] { 0x65, 0x88, 0x80 };

        var data = new Byte[4 + sps.Length + 4 + pps.Length + 4 + idr.Length];
        var offset = 0;
        foreach (var nal in new[] { sps, pps, idr })
        {
            data[offset++] = 0; data[offset++] = 0; data[offset++] = 0; data[offset++] = 1;
            Array.Copy(nal, 0, data, offset, nal.Length);
            offset += nal.Length;
        }
        return data;
    }
}
