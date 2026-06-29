using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegVP8EncoderTests
{
    [Fact(DisplayName = "FFmpegVP8Encoder ICodecInfo 正确")]
    public void CodecInfo_Correct()
    {
        var e = new FFmpegVP8Encoder();

        Assert.Equal("FFmpeg VP8 Encoder", e.Name);
        Assert.Equal("1.0.0", e.Version);
        Assert.Contains(VideoTypes.VP8, e.SupportedTypes);
        Assert.True(e.IsStateful);
    }

    [Fact(DisplayName = "FFmpegVP8Encoder 空帧返回空数据包")]
    public void Encode_EmptyFrames_ReturnsEmptyPacket()
    {
        var e = new FFmpegVP8Encoder();

        var result = e.Encode([]);

        Assert.NotNull(result);
        Assert.Equal(VideoTypes.VP8, result.CodecType);
    }

    [Fact(DisplayName = "FFmpegVP8Encoder null 帧返回空数据包")]
    public void Encode_NullFrames_ReturnsEmptyPacket()
    {
        var e = new FFmpegVP8Encoder();

        var result = e.Encode(null);

        Assert.NotNull(result);
        Assert.Equal(VideoTypes.VP8, result.CodecType);
    }

    [Fact(DisplayName = "FFmpegVP8Encoder Decode 抛 NotSupportedException")]
    public void Decode_ThrowsNotSupported()
    {
        var e = new FFmpegVP8Encoder();
        Assert.Throws<NotSupportedException>(() => e.Decode(null));
    }

    [Fact(DisplayName = "FFmpegVP8Encoder Dispose 可重复调用")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var e = new FFmpegVP8Encoder();
        e.Dispose();
        e.Dispose();
    }
}
