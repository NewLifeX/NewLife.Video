using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegVP8DecoderTests
{
    [Fact(DisplayName = "FFmpegVP8Decoder ICodecInfo 正确")]
    public void CodecInfo_Correct()
    {
        var d = new FFmpegVP8Decoder();

        Assert.Equal("FFmpeg VP8 Decoder", d.Name);
        Assert.Equal("1.0.0", d.Version);
        Assert.Contains(VideoTypes.VP8, d.SupportedTypes);
        Assert.True(d.IsStateful);
    }

    [Fact(DisplayName = "FFmpegVP8Decoder 空包返回空")]
    public void Decode_NullPacket_ReturnsEmpty()
    {
        var d = new FFmpegVP8Decoder();
        Assert.Empty(d.Decode(null));
    }

    [Fact(DisplayName = "FFmpegVP8Decoder 空数据包返回空")]
    public void Decode_EmptyDataPacket_ReturnsEmpty()
    {
        var d = new FFmpegVP8Decoder();
        var packet = new VPacket { Data = new ArrayPacket([]), CodecType = VideoTypes.VP8 };

        var result = d.Decode(packet);
        Assert.Empty(result);
    }

    [Fact(DisplayName = "FFmpegVP8Decoder Encode 抛 NotSupportedException")]
    public void Encode_ThrowsNotSupported()
    {
        var d = new FFmpegVP8Decoder();
        Assert.Throws<NotSupportedException>(() => d.Encode(null));
    }

    [Fact(DisplayName = "FFmpegVP8Decoder Dispose 可重复调用")]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var d = new FFmpegVP8Decoder();
        d.Dispose();
        d.Dispose(); // 不应抛异常
    }

    [Fact(DisplayName = "FFmpegVP8Decoder Decode 返回数据包为空时返回空数组")]
    public void Decode_DataPacketNullData_ReturnsEmpty()
    {
        var d = new FFmpegVP8Decoder();
        var packet = new VPacket { Data = null, CodecType = VideoTypes.VP8 };

        var result = d.Decode(packet);
        Assert.Empty(result);
    }
}
