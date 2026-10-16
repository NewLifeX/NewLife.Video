using NewLife.Data;
using NewLife.Video;
using NewLife.Video.ChipHeaders;
using NewLife.Video.Codecs;
using NewLife.Video.Converters;
using NewLife.Video.Devices;
using NewLife.Video.Flv;
using NewLife.Video.FMp4;
using NewLife.Video.Processing;
using NewLife.Video.Streaming;
using NewLife.Video.Ts;

// AOT 冒烟测试：以纯托管路径覆盖各模块核心场景，验证 NativeAOT 编译与运行期兼容性。
// 任一场景失败以非 0 退出码结束，作为 CI 门禁信号。

var passed = 0;
var failures = new List<String>();

void Check(String name, Action action)
{
    try
    {
        action();
        passed++;
        Console.WriteLine($"  [PASS] {name}");
    }
    catch (Exception ex)
    {
        failures.Add(name);
        Console.WriteLine($"  [FAIL] {name}: {ex.GetType().Name}: {ex.Message}");
    }
}

static void Ensure(Boolean condition, String message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// 构造含 SPS/PPS/IDR 的 H.264 Annex-B 关键帧数据（与单元测试使用同一份样例）
static Byte[] MakeAnnexBKeyFrame()
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

static String TempPath(String ext) => Path.Combine(Path.GetTempPath(), $"aotsmoke_{Guid.NewGuid():N}{ext}");

Console.WriteLine("===== AotSmoke：NativeAOT 兼容冒烟 =====");
Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} / {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");

Check("基础类型 VideoFrame/VPacket", () =>
{
    var frame = VideoFrame.Create(32, 24, VideoTypes.RGB24);
    Ensure(frame.Width == 32 && frame.Height == 24, "宽高错误");
    Ensure(frame.Data is { Length: > 0 }, "帧数据为空");

    var packet = new VPacket { Data = new ArrayPacket(new Byte[16]), CodecType = VideoTypes.H264, PTS = 1 };
    Ensure(packet.CodecType == VideoTypes.H264, "包类型错误");
    Ensure(packet.Data.Total == 16, "包长度错误");
});

Check("视频格式检测 VideoFormatDetector", () =>
{
    Ensure(VideoFormatDetector.Detect(new Byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) == VideoTypes.MJPEG, "JPEG 检测错误");
    Ensure(VideoFormatDetector.Detect(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x67 }) == VideoTypes.H264, "H264 检测错误");
});

Check("MJPEG 编解码往返", () =>
{
    var codec = new MjpegCodec();
    var frame = VideoFrame.Create(64, 48, VideoTypes.RGB24);
    for (var i = 0; i < frame.Data.Length; i++)
    {
        frame.Data[i] = (Byte)(i * 7);
    }

    var encoded = codec.Encode([frame]);
    Ensure(encoded?.Data != null, "编码结果为空");

    var decoded = codec.Decode(encoded);
    Ensure(decoded.Length > 0, "解码结果为空");
    Ensure(decoded[0].Width == 64 && decoded[0].Height == 48, "解码尺寸错误");
});

Check("工厂注册 + 芯片头解包链", () =>
{
    var codec = new MjpegCodec();
    var factory = new VideoCodecFactory();
    factory.Register(VideoTypes.MJPEG, codec);
    factory.RegisterChipHeader(new HisiliconVideoHeader());

    var source = VideoFrame.Create(48, 32, VideoTypes.RGB24);
    var encoded = codec.Encode([source]);
    Ensure(encoded?.Data != null, "编码结果为空");

    var header = new HisiliconVideoHeader();
    Ensure(header.TryAdd(encoded.Data, out var withHeader), "海思加头失败");

    var frames = factory.Decode(new VPacket { Data = withHeader, CodecType = VideoTypes.MJPEG });
    Ensure(frames.Length > 0, "工厂解码失败");
});

Check("芯片头 海思/大华/宇视 TryAdd→TryTrim 往返", () =>
{
    var payload = new ArrayPacket(new Byte[] { 0x01, 0x02, 0x03, 0x04 });
    foreach (IVideoChipHeader header in new IVideoChipHeader[] { new HisiliconVideoHeader(), new DahuaVideoHeader(), new UniviewVideoHeader() })
    {
        Ensure(header.TryAdd(payload, out var withHeader), $"{header.GetType().Name} 加头失败");
        Ensure(header.TryTrim(withHeader, out var restored), $"{header.GetType().Name} 去头失败");
        Ensure(restored.Total == payload.Total, $"{header.GetType().Name} 长度不符");
    }
});

Check("H.264 位流工具 ExpGolombReader/NALUHeader", () =>
{
    var reader = new ExpGolombReader(new Byte[] { 0xAB, 0xCD, 0xEF, 0x01 });
    Ensure(reader.ReadBits(8) == 0xAB, "ReadBits 错误");
    Ensure(reader.ReadBits(8) == 0xCD, "ReadBits 第二字节错误");

    var header = new NALUHeader(0x67);
    Ensure(header.NalUnitType == NalUnitType.SPS, "NAL 类型错误");
});

Check("色彩空间 YUV420P↔RGB24", () =>
{
    const Int32 w = 16, h = 16;
    var y = new Byte[w * h];
    var u = new Byte[w * h / 4];
    var v = new Byte[w * h / 4];
    Array.Fill(y, (Byte)128);
    Array.Fill(u, (Byte)128);
    Array.Fill(v, (Byte)128);

    var rgb = ColorSpaceConverter.YUV420PToRGB24(y, u, v, w, h);
    Ensure(rgb.Length == w * h * 3, "RGB 长度错误");

    var (y2, u2, v2) = ColorSpaceConverter.RGB24ToYUV420P(rgb, w, h);
    Ensure(y2.Length == w * h && u2.Length == w * h / 4 && v2.Length == w * h / 4, "YUV 长度错误");

    ColorSpaceConverter.YUVToRGB(128, 128, 128, out var r, out var g, out var b);
    Ensure(r == g && g == b, "灰色转换不一致");
});

Check("处理管线 缩放/裁剪/旋转/调色/水印/时间戳", () =>
{
    var pipeline = new VideoPipeline()
        .Append(new FrameResizer(32, 24))
        .Append(new FrameCropper(0, 0, 16, 12))
        .Append(new FrameRotator(FrameRotator.RotationAngle.Clockwise90))
        .Append(new ColorAdjuster(brightness: 0.1))
        .Append(new WatermarkOverlay("AOT"))
        .Append(new TimestampOverlay("HH:mm:ss"));

    var input = VideoFrame.Create(64, 48, VideoTypes.RGB24);
    var output = pipeline.ProcessFrame(input);
    Ensure(output.Width == 12 && output.Height == 16, $"管线尺寸错误: {output.Width}x{output.Height}");
});

Check("运动检测 MotionDetector", () =>
{
    const Int32 w = 32, h = 32;
    var detector = new MotionDetector { Threshold = 20, MinArea = 5 };
    var frame1 = VideoFrame.Create(w, h, VideoTypes.YUV420P);
    var frame2 = VideoFrame.Create(w, h, VideoTypes.YUV420P);
    Array.Fill(frame1.Data, (Byte)16);
    Array.Fill(frame2.Data, (Byte)16);
    for (var y = 14; y < 18; y++)
    {
        for (var x = 14; x < 18; x++)
        {
            frame2.Data[y * w + x] = 200;
        }
    }

    detector.Detect(frame1);
    var regions = detector.Detect(frame2);
    Ensure(regions.Length > 0, "未检测到运动区域");
});

Check("快照编码 JPEG/PNG/WebP/BMP", () =>
{
    var frame = VideoFrame.Create(32, 32, VideoTypes.RGB24);
    for (var i = 0; i < frame.Data.Length; i++)
    {
        frame.Data[i] = (Byte)(i * 5);
    }

    Ensure(SnapshotEncoder.EncodeToJpeg(frame).Length > 0, "JPEG 为空");
    Ensure(SnapshotEncoder.EncodeToPng(frame).Length > 0, "PNG 为空");
    Ensure(SnapshotEncoder.EncodeToWebP(frame).Length > 0, "WebP 为空");
    Ensure(SnapshotEncoder.EncodeToBmp(frame).Length > 0, "BMP 为空");
});

Check("纯托管编码器 GIF/动画GIF/PNG/WebP", () =>
{
    const Int32 w = 16, h = 16;
    var rgb = new Byte[w * h * 3];
    for (var i = 0; i < rgb.Length; i++)
    {
        rgb[i] = (Byte)(i & 0xFF);
    }

    var gif = GifEncoder.Encode(rgb, w, h);
    Ensure(gif.Length > 0 && gif[0] == (Byte)'G', "GIF 文件头错误");

    var anim = GifEncoder.EncodeAnimation([rgb, rgb], w, h, 10, 0);
    Ensure(anim.Length > gif.Length, "动画 GIF 应大于单帧");

    var png = PngEncoder.Encode(rgb, w, h);
    Ensure(png.Length > 0 && png[0] == 0x89, "PNG 文件头错误");

    var webp = WebPEncoder.Encode(rgb, w, h);
    Ensure(webp.Length > 0, "WebP 为空");
});

Check("FLV 写入→读取往返", () =>
{
    var path = TempPath(".flv");
    try
    {
        using (var w = new FlvWriter(path))
        {
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(MakeAnnexBKeyFrame()), CodecType = VideoTypes.H264, IsKeyFrame = true, PTS = 0 });
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x80 }), CodecType = VideoTypes.H264, PTS = 40 });
        }

        using var r = new FlvReader(path);
        Ensure(r.VideoCodecType == VideoTypes.H264, "编码类型错误");
        Ensure(r.VideoWidth > 0 && r.VideoHeight > 0, "宽高解析失败");
        Ensure(r.TotalFrames >= 2, "帧数不足");
    }
    finally { try { File.Delete(path); } catch { } }
});

Check("fMP4 分段写入（内存）", () =>
{
    using var ms = new MemoryStream();
    using (var writer = new Fmp4SegmentWriter(ms, segmentFrames: 1, leaveOpen: true))
    {
        writer.WriteMetadata(320, 240, VideoTypes.H264, 25);
        writer.WriteFrame(new VPacket(new ArrayPacket(MakeAnnexBKeyFrame()), VideoTypes.H264));
    }

    Ensure(ms.Length > 0, "fMP4 数据为空");
});

Check("MP4 写入→读取往返", () =>
{
    var path = TempPath(".mp4");
    try
    {
        using (var w = new Mp4Writer(path))
        {
            w.WriteMetadata(320, 240, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(MakeAnnexBKeyFrame()), CodecType = VideoTypes.H264, IsKeyFrame = true, PTS = 0 });
            w.WriteFrame(new VPacket { Data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x9A, 0x80 }), CodecType = VideoTypes.H264, PTS = 40 });
        }

        using var r = new Mp4Reader(path);
        Ensure(r.VideoCodecType == VideoTypes.H264, "编码类型错误");
        Ensure(r.VideoWidth == 320 && r.VideoHeight == 240, $"宽高错误: {r.VideoWidth}x{r.VideoHeight}");

        // 帧数据帧级往返：stco 绝对偏移 + 长度前缀样本
        var f1 = r.ReadFrame();
        Ensure(f1?.Data != null && f1.Data.Total > 0 && f1.IsKeyFrame, "首帧读取失败");
        var f2 = r.ReadFrame();
        Ensure(f2?.Data != null && f2.Data.Total > 0, "次帧读取失败");
    }
    finally { try { File.Delete(path); } catch { } }
});

Check("TS 写入→读取往返", () =>
{
    var path = TempPath(".ts");
    try
    {
        using (var w = new TsWriter(path))
        {
            w.WriteMetadata(640, 480, VideoTypes.H264, 25);
            w.WriteFrame(new VPacket { Data = new ArrayPacket(MakeAnnexBKeyFrame()), CodecType = VideoTypes.H264, IsKeyFrame = true, PTS = 0 });
            w.Flush();
        }

        using var r = new TsReader(path);
        Ensure(r.VideoCodecType == VideoTypes.H264, "编码类型错误");
    }
    finally { try { File.Delete(path); } catch { } }
});

Check("HLS 分段 + 播放列表", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "aotsmoke_hls_" + Guid.NewGuid().ToString("N"));
    try
    {
        var seg = new HlsSegmenter(dir, "live", 500);
        seg.AddData(new Byte[100], 0, true);
        seg.AddData(new Byte[100], 200, false);
        seg.AddData(new Byte[100], 600, true);

        var playlist = seg.GeneratePlaylist(3);
        Ensure(playlist.Contains("#EXTM3U") && playlist.Contains("#EXTINF:"), "播放列表内容错误");
    }
    finally { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }
});

Check("JitterBuffer 乱序重排", () =>
{
    var buffer = new JitterBuffer(100);
    buffer.Enqueue(new VPacket { PTS = 30 });
    buffer.Enqueue(new VPacket { PTS = 0 });
    buffer.Enqueue(new VPacket { PTS = 60 });

    Ensure(buffer.Dequeue(100)?.PTS == 0, "首选帧错误");
    Ensure(buffer.Dequeue(130)?.PTS == 30, "次要帧错误");
    Ensure(buffer.Dequeue(160)?.PTS == 60, "末帧错误");
});

Check("RTP H.264 封包/解包往返", () =>
{
    var packetizer = new RtpH264Packetizer(1400);
    var nal = new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0xCD };

    var packets = packetizer.Packetize([nal], 90000, 0);
    var nals = packetizer.Depacketize(packets);
    Ensure(nals.Length == 1, "解包 NAL 数量错误");
    Ensure(nals[0].Length == nal.Length, "解包 NAL 长度错误");
});

Check("RtspClient/RtmpClient 构造（不连接）", () =>
{
    var rtsp = new RtspClient("rtsp://127.0.0.1/stream");
    Ensure(rtsp.Url == "rtsp://127.0.0.1/stream", "RtspClient URL 错误");

    var rtmp = new RtmpClient("rtmp://127.0.0.1/live/test");
    Ensure(rtmp.Url == "rtmp://127.0.0.1/live/test", "RtmpClient URL 错误");
});

Check("FFmpegInvoker 构造（不拉起进程）", () =>
{
    var invoker = new NewLife.Video.FFmpeg.FFmpegInvoker();
    Ensure(invoker.FFmpegPath != null, "FFmpegPath 为空");
});

Check("VideoDeviceManager/ScreenCapture 枚举", () =>
{
    var manager = new VideoDeviceManager();
    manager.RegisterBackend(new ScreenCaptureBackend());
    var devices = manager.GetDevices();
    Ensure(devices.Length > 0, "屏幕设备枚举为空");
});

Console.WriteLine($"===== 结果：通过 {passed}，失败 {failures.Count}，共 {passed + failures.Count} =====");
if (failures.Count > 0)
{
    foreach (var name in failures)
    {
        Console.WriteLine($"  失败场景: {name}");
    }
    return 1;
}

Console.WriteLine("全部场景通过");
return 0;
