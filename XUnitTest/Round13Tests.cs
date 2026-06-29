using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

public class Round13Tests
{
    [Fact(DisplayName = "AviWriter 生成标准AVI文件头部")]
    public void AviWriter_GeneratesValidHeader()
    {
        var path = Path.GetTempFileName() + ".avi";
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
    public void AviWriter_H264_UsesH264FourCC()
    {
        var path = Path.GetTempFileName() + ".avi";
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
    public void AviWriter_IncludesIdx1()
    {
        var path = Path.GetTempFileName() + ".avi";
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
    public void AviWriter_RoundTrip_WithReader()
    {
        var path = Path.GetTempFileName() + ".avi";
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

    [Fact(DisplayName = "VideoCodecFactory 自动注册MJPEG")]
    public void CodecFactory_AutoRegisters_Mjpeg()
    {
        var factory = new VideoCodecFactory();
        var codec = factory.Registry.GetCodec(VideoTypes.MJPEG);
        Assert.NotNull(codec);
        Assert.True(codec is ICodecInfo ci && ci.SupportedTypes.Contains(VideoTypes.MJPEG));
    }

    [Fact(DisplayName = "VideoCodecFactory 自动注册H264解码器")]
    public void CodecFactory_AutoRegisters_H264Decoder()
    {
        var factory = new VideoCodecFactory();
        Assert.True(factory.Registry.IsSupported(VideoTypes.H264));
    }

    [Fact(DisplayName = "VideoCodecFactory 自动注册H265解码器")]
    public void CodecFactory_AutoRegisters_H265Decoder()
    {
        var factory = new VideoCodecFactory();
        Assert.True(factory.Registry.IsSupported(VideoTypes.H265));
    }

    [Fact(DisplayName = "VideoCodecFactory 自动注册VP8/VP9解码器")]
    public void CodecFactory_AutoRegisters_VP8_VP9()
    {
        var factory = new VideoCodecFactory();
        Assert.True(factory.Registry.IsSupported(VideoTypes.VP8));
        Assert.True(factory.Registry.IsSupported(VideoTypes.VP9));
    }

    [Fact(DisplayName = "Amf0Encoder 编码String正确")]
    public void Amf0Encoder_String_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteString("hello");
        var data = enc.ToArray();
        // type marker (0x02) + 2-byte length + "hello"
        Assert.Equal(0x02, data[0]);
        Assert.Equal(0x00, data[1]);
        Assert.Equal(0x05, data[2]); // length = 5
        Assert.Equal((Byte)'h', data[3]);
    }

    [Fact(DisplayName = "Amf0Encoder 编码Number正确")]
    public void Amf0Encoder_Number_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteNumber(42.0);
        var data = enc.ToArray();
        Assert.Equal(0x00, data[0]); // type marker
        Assert.Equal(9, data.Length); // 1 + 8
    }

    [Fact(DisplayName = "Amf0Encoder 编码Null正确")]
    public void Amf0Encoder_Null_Correct()
    {
        var enc = new Amf0Encoder();
        enc.WriteNull();
        var data = enc.ToArray();
        Assert.Equal(0x05, data[0]);
        Assert.Equal(1, data.Length);
    }

    [Fact(DisplayName = "RtmpClient URL解析正确")]
    public void RtmpClient_UrlParsing_Correct()
    {
        var client = new RtmpClient("rtmp://192.168.1.100/live/stream1");
        Assert.Equal("192.168.1.100", client.Host);
        Assert.Equal(1935, client.Port);
        Assert.Equal("live", client.AppName);
        Assert.Equal("stream1", client.StreamName);
    }

    [Fact(DisplayName = "RtmpClient 带端口URL解析")]
    public void RtmpClient_UrlWithPort_Correct()
    {
        var client = new RtmpClient("rtmp://server:8080/app/stream");
        Assert.Equal("server", client.Host);
        Assert.Equal(8080, client.Port);
        Assert.Equal("app", client.AppName);
    }

    [Fact(DisplayName = "RtmpClient 初始未连接")]
    public void RtmpClient_InitiallyNotConnected()
    {
        var client = new RtmpClient("rtmp://localhost/app/stream");
        Assert.False(client.IsConnected);
    }
}
