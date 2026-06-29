using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegVP9DecoderTests
{
    [Fact(DisplayName = "FFmpegVP9Decoder ICodecInfo 正确")]
    public void CodecInfo_Correct()
    {
        var d = new FFmpegVP9Decoder();

        Assert.Equal("FFmpeg VP9 Decoder", d.Name);
        Assert.Equal("1.0.0", d.Version);
        Assert.Contains(VideoTypes.VP9, d.SupportedTypes);
        Assert.True(d.IsStateful);
    }

    [Fact(DisplayName = "FFmpegVP9Decoder 空包返回空")]
    public void Decode_NullPacket_ReturnsEmpty()
    {
        var d = new FFmpegVP9Decoder();
        Assert.Empty(d.Decode(null));
    }

    [Fact(DisplayName = "FFmpegVP9Decoder 空数据包返回空")]
    public void Decode_EmptyDataPacket_ReturnsEmpty()
    {
        var d = new FFmpegVP9Decoder();
        var packet = new VPacket { Data = new ArrayPacket([]), CodecType = VideoTypes.VP9 };

        var result = d.Decode(packet);
        Assert.Empty(result);
    }

    [Fact(DisplayName = "FFmpegVP9Decoder Encode 抛 NotSupportedException")]
    public void Encode_ThrowsNotSupported()
    {
        var d = new FFmpegVP9Decoder();
        Assert.Throws<NotSupportedException>(() => d.Encode(null));
    }

    [Fact(DisplayName = "FFmpegVP9Decoder Dispose 可重复调用")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var d = new FFmpegVP9Decoder();
        d.Dispose();
        d.Dispose();
    }

    [Fact(DisplayName = "FFmpegVP9Decoder Decode 返回数据包为空时返回空数组")]
    public void Decode_DataPacketNullData_ReturnsEmpty()
    {
        var d = new FFmpegVP9Decoder();
        var packet = new VPacket { Data = null, CodecType = VideoTypes.VP9 };

        var result = d.Decode(packet);
        Assert.Empty(result);
    }
}
