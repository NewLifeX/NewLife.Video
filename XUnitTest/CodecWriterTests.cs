using NewLife.Video;
using NewLife.Video.Codecs;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest;

public class CodecWriterTests
{
    [Fact(DisplayName = "FFmpegVP8Decoder ICodecInfo正确")]
    public void VP8Decoder_ICodecInfo_Correct()
    {
        var d = new FFmpegVP8Decoder();
        Assert.Contains(VideoTypes.VP8, d.SupportedTypes);
        Assert.True(d.IsStateful);
    }

    [Fact(DisplayName = "FFmpegVP8Decoder 空包返回空")]
    public void VP8Decoder_Empty_ReturnsEmpty()
    {
        Assert.Empty(new FFmpegVP8Decoder().Decode(null));
    }

    [Fact(DisplayName = "FFmpegVP9Decoder ICodecInfo正确")]
    public void VP9Decoder_ICodecInfo_Correct()
    {
        var d = new FFmpegVP9Decoder();
        Assert.Contains(VideoTypes.VP9, d.SupportedTypes);
    }

    [Fact(DisplayName = "TsWriter 写入帧正确")]
    public void TsWriter_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".ts";
        try
        {
            using var w = new TsWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[500]), CodecType = VideoTypes.H264 });
            w.Flush();

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "FlvWriter 写入帧正确")]
    public void FlvWriter_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            using var w = new FlvWriter(path);
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[200]), CodecType = VideoTypes.H264, IsKeyFrame = true });
            w.Flush();

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "Mp4Writer 写入帧正确")]
    public void Mp4Writer_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            using (var w = new Mp4Writer(path))
            {
                w.WriteMetadata(320, 240, VideoTypes.H264, 25);
                w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[300]), CodecType = VideoTypes.H264 });
            }

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviWriter 写入帧正确")]
    public void AviWriter_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".avi";
        try
        {
            using var w = new AviWriter(path);
            w.WriteMetadata(320, 240, VideoTypes.MJPEG, 25);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[150]), CodecType = VideoTypes.MJPEG });
            w.Flush();

            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "RawVideoWriter 写入YUV帧正确")]
    public void RawVideoWriter_WriteFrame_Works()
    {
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            using var w = new RawVideoWriter(path);
            w.WriteMetadata(320, 240, VideoTypes.YUV420P, 25);
            var frameSize = VideoFrame.CalculateDataSize(320, 240, VideoTypes.YUV420P);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[frameSize]), CodecType = VideoTypes.YUV420P });
            w.Flush();

            Assert.Equal(frameSize, new FileInfo(path).Length);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "容器写入器 Disposable 安全")]
    public void Writers_DisposeSafe()
    {
        var path = Path.GetTempFileName();
        var writers = new IVideoContainerWriter[]
        {
            new Mp4Writer(path + ".mp4"),
            new FlvWriter(path + ".flv"),
            new AviWriter(path + ".avi"),
            new TsWriter(path + ".ts"),
            new RawVideoWriter(path + ".yuv"),
        };

        foreach (var w in writers)
        {
            w.WriteMetadata(64, 48, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new NewLife.Data.Packet(new Byte[10]), CodecType = VideoTypes.H264 });
            w.Dispose();
        }

        foreach (var ext in new[] { ".mp4", ".flv", ".avi", ".ts", ".yuv" })
            try { if (File.Exists(path + ext)) File.Delete(path + ext); } catch { }
    }
}
