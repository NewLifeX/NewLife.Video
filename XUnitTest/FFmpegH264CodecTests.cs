using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest;

public class FFmpegH264CodecTests
{
    [Fact(DisplayName = "FFmpegH264Decoder ICodecInfo 正确")]
    public void Decoder_ICodecInfo_Correct()
    {
        var decoder = new FFmpegH264Decoder();

        Assert.Equal("FFmpeg H.264 Decoder", decoder.Name);
        Assert.Contains(VideoTypes.H264, decoder.SupportedTypes);
        Assert.True(decoder.IsStateful);
    }

    [Fact(DisplayName = "FFmpegH264Decoder 已知尺寸构造")]
    public void Decoder_KnownDimensions_StoresCorrectly()
    {
        var decoder = new FFmpegH264Decoder(width: 1920, height: 1080);

        Assert.Equal(1920, decoder.Width);
        Assert.Equal(1080, decoder.Height);
    }

    [Fact(DisplayName = "FFmpegH264Decoder 解码空包返回空")]
    public void Decode_EmptyPacket_ReturnsEmpty()
    {
        var decoder = new FFmpegH264Decoder();

        Assert.Empty(decoder.Decode(null));
        Assert.Empty(decoder.Decode(new VPacket()));
    }

    [Fact(DisplayName = "FFmpegH264Decoder 编码抛异常")]
    public void Decoder_Encode_ThrowsNotSupported()
    {
        var decoder = new FFmpegH264Decoder();

        Assert.Throws<NotSupportedException>(() => decoder.Encode(null));
    }

    [Fact(DisplayName = "FFmpegH264Encoder ICodecInfo 正确")]
    public void Encoder_ICodecInfo_Correct()
    {
        var encoder = new FFmpegH264Encoder(640, 480);

        Assert.Equal("FFmpeg H.264 Encoder", encoder.Name);
        Assert.Contains(VideoTypes.H264, encoder.SupportedTypes);
        Assert.True(encoder.IsStateful);
    }

    [Fact(DisplayName = "FFmpegH264Encoder 解码抛异常")]
    public void Encoder_Decode_ThrowsNotSupported()
    {
        var encoder = new FFmpegH264Encoder(640, 480);

        Assert.Throws<NotSupportedException>(() => encoder.Decode(null));
    }

    [Fact(DisplayName = "FFmpegH264Encoder 编码空帧抛异常")]
    public void Encoder_EmptyFrames_Throws()
    {
        var encoder = new FFmpegH264Encoder(640, 480);

        Assert.Throws<ArgumentNullException>(() => encoder.Encode(null));
        Assert.Throws<ArgumentNullException>(() => encoder.Encode([]));
    }

    [Fact(DisplayName = "FFmpegH264Encoder 编码不支持的格式抛异常")]
    public void Encoder_UnsupportedFormat_Throws()
    {
        var encoder = new FFmpegH264Encoder(640, 480);
        var frame = VideoFrame.Create(640, 480, VideoTypes.RGB24);

        Assert.Throws<NotSupportedException>(() => encoder.Encode([frame]));
    }
}
