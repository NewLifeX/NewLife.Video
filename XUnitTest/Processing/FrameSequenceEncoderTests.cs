using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FrameSequenceEncoderTests
{
    [Fact(DisplayName = "FrameSequenceEncoder 构造默认参数")]
    public void Constructor_DefaultParameters()
    {
        var encoder = new FrameSequenceEncoder(640, 480);
        Assert.NotNull(encoder);
    }

    [Fact(DisplayName = "FrameSequenceEncoder 构造指定编码类型")]
    public void Constructor_WithCodecType()
    {
        var encoder = new FrameSequenceEncoder(1920, 1080, 30.0, VideoTypes.H265);
        Assert.NotNull(encoder);
    }

    [Fact(DisplayName = "FrameSequenceEncoder Encode 空帧序列返回空包")]
    public void Encode_EmptyFrames_ReturnsEmptyPacket()
    {
        var encoder = new FrameSequenceEncoder(640, 480, 25.0, VideoTypes.MJPEG);

        var result = encoder.Encode([]);

        Assert.NotNull(result);
    }

    [Fact(DisplayName = "FrameSequenceEncoder Encode null 帧序列抛 ArgumentNullException")]
    public void Encode_NullFrames_ThrowsArgumentNull()
    {
        var encoder = new FrameSequenceEncoder(640, 480);

        Assert.Throws<ArgumentNullException>(() => encoder.Encode(null));
    }

    [Fact(DisplayName = "FrameSequenceEncoder Encode 不支持编码类型抛 NotSupportedException")]
    public void Encode_UnsupportedCodec_ThrowsNotSupported()
    {
        var encoder = new FrameSequenceEncoder(640, 480, 25.0, VideoTypes.VP8);
        var frame = VideoFrame.Create(640, 480, VideoTypes.YUV420P);

        Assert.Throws<NotSupportedException>(() => encoder.Encode([frame]));
    }

    [Fact(DisplayName = "FrameSequenceEncoder Encode MJPEG 编码单帧")]
    public void Encode_MJPEG_SingleFrame()
    {
        var encoder = new FrameSequenceEncoder(64, 48, 10.0, VideoTypes.MJPEG);
        var frame = VideoFrame.Create(64, 48, VideoTypes.YUV420P);

        var result = encoder.Encode([frame]);

        Assert.NotNull(result);
        Assert.Equal(VideoTypes.MJPEG, result.CodecType);
    }

    [Fact(DisplayName = "FrameSequenceEncoder EncodeFromFiles 空路径返回空包")]
    public void EncodeFromFiles_EmptyPaths_ReturnsEmptyPacket()
    {
        var encoder = new FrameSequenceEncoder(64, 48, 10.0, VideoTypes.MJPEG);

        var result = encoder.EncodeFromFiles([]);

        Assert.NotNull(result);
    }

    [Fact(DisplayName = "FrameSequenceEncoder EncodeFromFiles 不存在的文件跳过")]
    public void EncodeFromFiles_NonExistent_Skipped()
    {
        var encoder = new FrameSequenceEncoder(64, 48, 10.0, VideoTypes.MJPEG);

        var result = encoder.EncodeFromFiles(["Z:\\nonexistent.jpg"]);

        Assert.NotNull(result);
    }
}
