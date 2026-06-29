using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using NewLife.Video.Containers;
using NewLife.Video.Processing;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

/// <summary>集成测试：验证编解码器+容器+处理管线的端到端联动</summary>
public class IntegrationTests
{
    [Fact(DisplayName = "集成: MJPEG编解码→RawVideoReader往返")]
    public void MJpegCodec_RawVideoReader_RoundTrip()
    {
        var codec = new MjpegCodec();
        var original = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        original.Timestamp = 100;

        // 编码
        var encoded = codec.Encode([original]);
        Assert.NotNull(encoded);

        // 解码
        var decoded = codec.Decode(encoded);
        Assert.NotEmpty(decoded);
        Assert.Equal(original.Width, decoded[0].Width);
        Assert.Equal(original.Height, decoded[0].Height);
    }

    [Fact(DisplayName = "集成: 处理管线缩放+裁剪+色彩调整")]
    public void Pipeline_ResizeCropAdjust_Chain()
    {
        var pipeline = new VideoPipeline()
            .Append(new FrameResizer(32, 24))
            .Append(new FrameCropper(0, 0, 16, 12))
            .Append(new ColorAdjuster(brightness: 0.1));

        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(16, output.Width);
        Assert.Equal(12, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "集成: 旋转+水印+时间戳叠加")]
    public void Pipeline_RotateWatermarkTimestamp_Chain()
    {
        var pipeline = new VideoPipeline()
            .Append(new FrameRotator(FrameRotator.RotationAngle.Clockwise90))
            .Append(new WatermarkOverlay("TEST"))
            .Append(new TimestampOverlay("HH:mm:ss"));

        var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        var output = pipeline.ProcessFrame(input);

        Assert.Equal(48, output.Width); // 90度旋转后宽高互换
        Assert.Equal(64, output.Height);
    }

    [Fact(DisplayName = "集成: 编码格式检测+工厂解码")]
    public void FormatDetect_FactoryDecode_Works()
    {
        // 检测 JPEG 格式
        var jpegData = new Byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        Assert.Equal(VideoTypes.MJPEG, VideoFormatDetector.Detect(jpegData));

        // 检测 H.264 格式
        var h264Data = new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x67 };
        Assert.Equal(VideoTypes.H264, VideoFormatDetector.Detect(h264Data));

        // 通过工厂使用检测到的类型
        var factory = new VideoCodecFactory();
        factory.Register(VideoTypes.MJPEG, new MjpegCodec());

        var packet = new VPacket
        {
            Data = new ArrayPacket(jpegData),
            CodecType = VideoFormatDetector.Detect(jpegData),
        };

        var frames = factory.Decode(packet);
        Assert.NotEmpty(frames);
    }

    [Fact(DisplayName = "集成: 运动检测+帧差法")]
    public void MotionDetect_TwoFrames_DetectsChange()
    {
        var detector = new MotionDetector { Threshold = 20, MinArea = 5 };
        var w = 32; var h = 32;

        var frame1 = VideoFrame.Create(w, h, VideoTypes.YUV420P);
        var frame2 = VideoFrame.Create(w, h, VideoTypes.YUV420P);

        Array.Fill(frame1.Data, (Byte)16);
        Array.Fill(frame2.Data, (Byte)16);
        // 修改中间区域模拟运动
        for (var y = 14; y < 18; y++)
            for (var x = 14; x < 18; x++)
                frame2.Data[y * w + x] = 200;

        detector.Detect(frame1);
        var regions = detector.Detect(frame2);

        Assert.NotEmpty(regions);
        Assert.True(regions[0].Area >= 5);
    }

    [Fact(DisplayName = "集成: RTP封包解包往返")]
    public void RTP_PacketizeDepacketize_RoundTrip()
    {
        var packetizer = new RtpH264Packetizer(1400);
        var nal = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0xCD };

        var packets = packetizer.Packetize([nal], 90000, 0);
        var nals = packetizer.Depacketize(packets);

        Assert.Single(nals);
        Assert.Equal(nal, nals[0]);
    }

    [Fact(DisplayName = "集成: HLS分段+播放列表完整流程")]
    public void HLS_SegmentAndPlaylist_Complete()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hls_int_" + Guid.NewGuid());
        try
        {
            var seg = new HlsSegmenter(dir, "live", 500);
            seg.AddData(new Byte[100], 0, true);
            seg.AddData(new Byte[100], 200, false);
            seg.AddData(new Byte[100], 600, true);

            var playlist = seg.GeneratePlaylist(3);
            Assert.Contains("#EXTM3U", playlist);
            Assert.Contains("#EXTINF:", playlist);
            Assert.Contains(".ts", playlist);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact(DisplayName = "集成: JitterBuffer乱序重排")]
    public void JitterBuffer_Reorder_Works()
    {
        var buffer = new JitterBuffer(100);

        buffer.Enqueue(new VPacket { PTS = 30 });
        buffer.Enqueue(new VPacket { PTS = 0 });
        buffer.Enqueue(new VPacket { PTS = 60 });

        var f1 = buffer.Dequeue(100);
        var f2 = buffer.Dequeue(130);
        var f3 = buffer.Dequeue(160);

        Assert.Equal(0, f1?.PTS);
        Assert.Equal(30, f2?.PTS);
        Assert.Equal(60, f3?.PTS);
    }
}
