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

    [Fact(DisplayName = "FlvWriter 首个关键帧自动写入 Sequence Header")]
    public void FirstKeyFrame_WritesSequenceHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flvtest_{Guid.NewGuid():N}.flv");
        try
        {
            // 先写入再关闭，确保文件完整落盘
            using (var w = new FlvWriter(path))
            {
                w.WriteMetadata(1920, 1080, VideoTypes.H264, 30);

                // 构造含 SPS/PPS 的 H.264 关键帧数据（起始码 + NAL）
                var sps = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0x40, 0x80 };
                var pps = new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };
                var idr = new Byte[] { 0x65, 0x88, 0x80 };

                var frameData = new Byte[4 + sps.Length + 4 + pps.Length + 4 + idr.Length];
                var offset = 0;
                frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 1;
                Array.Copy(sps, 0, frameData, offset, sps.Length); offset += sps.Length;
                frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 1;
                Array.Copy(pps, 0, frameData, offset, pps.Length); offset += pps.Length;
                frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 0; frameData[offset++] = 1;
                Array.Copy(idr, 0, frameData, offset, idr.Length);

                w.WriteFrame(new VPacket
                {
                    Data = new ArrayPacket(frameData),
                    CodecType = VideoTypes.H264,
                    IsKeyFrame = true,
                    PTS = 0,
                });

                w.WriteFrame(new VPacket
                {
                    Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x80 }),
                    CodecType = VideoTypes.H264,
                    IsKeyFrame = false,
                    PTS = 33,
                });
            }

            Assert.True(File.Exists(path), "文件应存在");
            Assert.True(new FileInfo(path).Length > 0, "文件不应为空");

            using var reader = new FlvReader(path);
            Assert.Equal(VideoTypes.H264, reader.VideoCodecType);
            Assert.True(reader.VideoWidth > 0, $"VideoWidth={reader.VideoWidth} 应大于0");
            Assert.True(reader.VideoHeight > 0, $"VideoHeight={reader.VideoHeight} 应大于0");
            Assert.True(reader.TotalFrames >= 2, $"TotalFrames={reader.TotalFrames} 应 >= 2");
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "FlvWriter H.265 序列头正确写入")]
    public void H265_SequenceHeader_Written()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            using var w = new FlvWriter(path);
            w.WriteMetadata(1920, 1080, VideoTypes.H265, 30);

            // 构造含 VPS/SPS/PPS 的 H.265 关键帧
            var vps = new Byte[] { 0x40, 0x01, 0x0C, 0x01, 0xFF, 0xFF, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9 };
            var sps = new Byte[] { 0x42, 0x01, 0x01, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9, 0xA0 };
            var pps = new Byte[] { 0x44, 0x01, 0xC0, 0x72, 0xB0, 0x46, 0x40 };
            var idr = new Byte[] { 0x26, 0x01, 0xAF, 0x80 };

            var ms = new MemoryStream();
            WriteNalWithStartCode(ms, vps);
            WriteNalWithStartCode(ms, sps);
            WriteNalWithStartCode(ms, pps);
            WriteNalWithStartCode(ms, idr);
            var frameData = ms.ToArray();

            w.WriteFrame(new VPacket
            {
                Data = new ArrayPacket(frameData),
                CodecType = VideoTypes.H265,
                IsKeyFrame = true,
                PTS = 0,
            });

            w.Flush();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "FlvWriter 仅非关键帧不触发序列头（容错）")]
    public void NonKeyFrame_NoSequenceHeader()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            using var w = new FlvWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);

            // 非关键帧不应触发序列头写入
            w.WriteFrame(new VPacket
            {
                Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A }),
                CodecType = VideoTypes.H264,
                IsKeyFrame = false,
                PTS = 0,
            });

            w.Flush();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    private static void WriteNalWithStartCode(MemoryStream ms, Byte[] nal)
    {
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(0);
        ms.WriteByte(1);
        ms.Write(nal, 0, nal.Length);
    }
}
