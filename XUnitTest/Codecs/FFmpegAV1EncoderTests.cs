using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegAV1EncoderTests
{
    [Fact(DisplayName = "FFmpegAV1Encoder 实现接口正确")]
    public void ImplementsInterfaces()
    {
        var encoder = new FFmpegAV1Encoder();

        Assert.IsAssignableFrom<IVideoCodec>(encoder);
        Assert.IsAssignableFrom<ICodecInfo>(encoder);
        Assert.Equal("FFmpeg AV1 Encoder", encoder.Name);
        Assert.Contains(VideoTypes.AV1, encoder.SupportedTypes);
        Assert.True(encoder.IsStateful);
    }

    [Fact(DisplayName = "FFmpegAV1Encoder 空帧返回空数据包")]
    public void Encode_EmptyFrames_ReturnsEmptyPacket()
    {
        var encoder = new FFmpegAV1Encoder(640, 480);
        var result = encoder.Encode([]);

        Assert.NotNull(result);
        Assert.Equal(VideoTypes.AV1, result.CodecType);
    }

    [Fact(DisplayName = "FFmpegAV1Encoder null帧返回空数据包")]
    public void Encode_NullFrames_ReturnsEmptyPacket()
    {
        var encoder = new FFmpegAV1Encoder();
        var result = encoder.Encode(null);

        Assert.NotNull(result);
        Assert.Equal(VideoTypes.AV1, result.CodecType);
    }

    [Fact(DisplayName = "FFmpegAV1Encoder 解码抛NotSupported")]
    public void Decode_ThrowsNotSupported()
    {
        var encoder = new FFmpegAV1Encoder();
        Assert.Throws<NotSupportedException>(() => encoder.Decode(new VPacket { CodecType = VideoTypes.AV1 }));
    }
}
