using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class FFmpegAV1DecoderTests
{
    [Fact(DisplayName = "FFmpegAV1Decoder 实现接口正确")]
    public void ImplementsInterfaces()
    {
        var decoder = new FFmpegAV1Decoder();

        Assert.IsAssignableFrom<IVideoCodec>(decoder);
        Assert.IsAssignableFrom<ICodecInfo>(decoder);
        Assert.Equal("FFmpeg AV1 Decoder", decoder.Name);
        Assert.Contains(VideoTypes.AV1, decoder.SupportedTypes);
        Assert.True(decoder.IsStateful);
    }

    [Fact(DisplayName = "FFmpegAV1Decoder 空数据返回空数组")]
    public void Decode_EmptyData_ReturnsEmpty()
    {
        var decoder = new FFmpegAV1Decoder();
        var result = decoder.Decode(new VPacket { Data = null, CodecType = VideoTypes.AV1 });
        Assert.Empty(result);
    }

    [Fact(DisplayName = "FFmpegAV1Decoder 编码抛NotSupported")]
    public void Encode_ThrowsNotSupported()
    {
        var decoder = new FFmpegAV1Decoder();
        Assert.Throws<NotSupportedException>(() => decoder.Encode([]));
    }

    [Fact(DisplayName = "FFmpegAV1Decoder 工厂注册可用")]
    public void Factory_Registration_Works()
    {
        var factory = new VideoCodecFactory();
        Assert.True(factory.Registry.IsSupported(VideoTypes.AV1));

        var codec = factory.Registry.GetCodec(VideoTypes.AV1);
        Assert.NotNull(codec);
        Assert.IsType<FFmpegAV1Decoder>(codec);
    }
}
