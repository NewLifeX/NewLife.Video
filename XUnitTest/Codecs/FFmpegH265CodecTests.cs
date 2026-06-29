using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegH265CodecTests
{
    [Fact(DisplayName = "FFmpegH265Decoder ICodecInfo正确")]
    public void Decoder_ICodecInfo_Correct()
    {
        var d = new FFmpegH265Decoder();
        Assert.Equal("FFmpeg H.265 Decoder", d.Name);
        Assert.Contains(VideoTypes.H265, d.SupportedTypes);
        Assert.True(d.IsStateful);
    }

    [Fact(DisplayName = "FFmpegH265Decoder 空包返回空")]
    public void Decode_Empty_ReturnsEmpty()
    {
        var d = new FFmpegH265Decoder();
        Assert.Empty(d.Decode(null));
        Assert.Empty(d.Decode(new VPacket()));
    }

    [Fact(DisplayName = "FFmpegH265Decoder 已知尺寸构造")]
    public void Decoder_Dimensions_Stored()
    {
        var d = new FFmpegH265Decoder(width: 3840, height: 2160);
        Assert.Equal(3840, d.Width);
        Assert.Equal(2160, d.Height);
    }

    [Fact(DisplayName = "FFmpegH265Decoder 编码抛异常")]
    public void Decoder_Encode_Throws() => Assert.Throws<NotSupportedException>(() => new FFmpegH265Decoder().Encode(null));

    [Fact(DisplayName = "FFmpegH265Encoder ICodecInfo正确")]
    public void Encoder_ICodecInfo_Correct()
    {
        var e = new FFmpegH265Encoder(640, 480);
        Assert.Contains(VideoTypes.H265, e.SupportedTypes);
        Assert.True(e.IsStateful);
    }

    [Fact(DisplayName = "FFmpegH265Encoder 解码抛异常")]
    public void Encoder_Decode_Throws() => Assert.Throws<NotSupportedException>(() => new FFmpegH265Encoder(640, 480).Decode(null));

    [Fact(DisplayName = "FFmpegH265Encoder 空帧抛异常")]
    public void Encoder_Empty_Throws()
    {
        var e = new FFmpegH265Encoder(640, 480);
        Assert.Throws<ArgumentNullException>(() => e.Encode(null));
        Assert.Throws<ArgumentNullException>(() => e.Encode([]));
    }

    [Fact(DisplayName = "FFmpegH265Encoder 非YUV格式抛异常")]
    public void Encoder_NonYUV_Throws()
    {
        var e = new FFmpegH265Encoder(640, 480);
        Assert.Throws<NotSupportedException>(() => e.Encode([VideoFrame.Create(640, 480, VideoTypes.RGB24)]));
    }
}
