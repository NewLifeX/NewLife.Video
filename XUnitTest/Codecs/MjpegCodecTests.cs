using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class MjpegCodecTests
{
    private readonly MjpegCodec _codec = new();

    [Fact(DisplayName = "MJPEG编解码器ICodecInfo正确")]
    public void ICodecInfo_Correct()
    {
        Assert.Equal("MJPEG", _codec.Name);
        Assert.Equal("1.0.0", _codec.Version);
        Assert.Contains(VideoTypes.MJPEG, _codec.SupportedTypes);
        Assert.False(_codec.IsStateful);
    }

    [Fact(DisplayName = "MJPEG解码非JPEG数据抛异常")]
    public void Decode_InvalidJpeg_Throws()
    {
        var data = new Byte[] { 0x00, 0x01, 0x02, 0x03 };
        var packet = new VPacket(new ArrayPacket(data), VideoTypes.MJPEG);

        Assert.Throws<InvalidOperationException>(() => _codec.Decode(packet));
    }

    [Fact(DisplayName = "MJPEG解码最小JPEG返回帧")]
    public void Decode_MinimalJpeg_ReturnsFrame()
    {
        // 构建最小 JPEG (SOI + EOI)
        var jpeg = new Byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var packet = new VPacket(new ArrayPacket(jpeg), VideoTypes.MJPEG);

        var frames = _codec.Decode(packet);

        Assert.NotNull(frames);
        Assert.NotEmpty(frames);
        Assert.Equal(VideoTypes.RGB24, frames[0].PixelFormat);
        Assert.True(frames[0].IsKeyFrame);
    }

    [Fact(DisplayName = "MJPEG编码RGB24帧返回JPEG数据")]
    public void Encode_RGB24Frame_ReturnsJpegPacket()
    {
        var frame = VideoFrame.Create(64, 48, VideoTypes.RGB24);
        // 填充测试颜色
        for (var i = 0; i < frame.Data.Length; i += 3)
        {
            frame.Data[i] = 255;     // B
            frame.Data[i + 1] = 0;   // G
            frame.Data[i + 2] = 0;   // R
        }

        var packet = _codec.Encode([frame]);

        Assert.NotNull(packet);
        Assert.NotNull(packet.Data);
        Assert.Equal(VideoTypes.MJPEG, packet.CodecType);
        Assert.True(packet.IsKeyFrame);

        // 验证 JPEG SOI 标记
        var jpegData = PacketHelper.ToArray(packet.Data);
        Assert.Equal(0xFF, jpegData[0]);
        Assert.Equal(0xD8, jpegData[1]);
    }

    [Fact(DisplayName = "MJPEG编码YUV420P帧自动转换为RGB")]
    public void Encode_YUV420PFrame_ConvertsToRgb()
    {
        var frame = VideoFrame.Create(32, 32, VideoTypes.YUV420P);
        // 填充测试数据（黑色 Y=16）
        for (var i = 0; i < frame.Width * frame.Height; i++)
            frame.Data[i] = 16;

        var packet = _codec.Encode([frame]);

        Assert.NotNull(packet);
        Assert.Equal(VideoTypes.MJPEG, packet.CodecType);

        // 验证有效 JPEG
        var jpegData = PacketHelper.ToArray(packet.Data);
        Assert.Equal(0xFF, jpegData[0]);
        Assert.Equal(0xD8, jpegData[1]);
    }

    [Fact(DisplayName = "MJPEG编解码往返基本一致")]
    public void EncodeDecode_RoundTrip_BasicConsistency()
    {
        var frame = VideoFrame.Create(64, 64, VideoTypes.RGB24);
        frame.Timestamp = 12345;
        frame.FrameIndex = 42;

        // 填充红色
        for (var i = 0; i < frame.Data.Length; i += 3)
        {
            frame.Data[i] = 0;     // B
            frame.Data[i + 1] = 0; // G
            frame.Data[i + 2] = 255; // R
        }

        var encoded = _codec.Encode([frame]);
        var decoded = _codec.Decode(encoded);

        Assert.NotNull(decoded);
        Assert.NotEmpty(decoded);
        Assert.Equal(frame.Width, decoded[0].Width);
        Assert.Equal(frame.Height, decoded[0].Height);
        Assert.Equal(frame.Timestamp, decoded[0].Timestamp);
        Assert.Equal(frame.FrameIndex, decoded[0].FrameIndex);
    }

    [Fact(DisplayName = "MJPEG编码不支持的像素格式抛异常")]
    public void Encode_UnsupportedFormat_Throws()
    {
        var frame = VideoFrame.Create(64, 48, VideoTypes.GRAY8);

        Assert.Throws<NotSupportedException>(() => _codec.Encode([frame]));
    }

    [Fact(DisplayName = "MJPEG编码空帧抛异常")]
    public void Encode_EmptyFrames_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _codec.Encode(null));
        Assert.Throws<ArgumentNullException>(() => _codec.Encode([]));
    }

    [Fact(DisplayName = "MJPEG解码空包返回空数组")]
    public void Decode_EmptyPacket_ReturnsEmpty()
    {
        Assert.Empty(_codec.Decode(null));
        Assert.Empty(_codec.Decode(new VPacket()));
    }
}
