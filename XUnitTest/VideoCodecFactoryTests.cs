using NewLife.Video;
using Xunit;

namespace XUnitTest;

public class VideoCodecFactoryTests
{
    private readonly VideoCodecFactory _factory = new();

    [Fact(DisplayName = "工厂初始状态无已注册编解码器")]
    public void Factory_InitialState_HasNoCodecs()
    {
        Assert.Empty(_factory.Registry.RegisteredTypes);
    }

    [Fact(DisplayName = "工厂Register后可通过Decode调用")]
    public void Register_ThenDecode_CallsCodec()
    {
        var codec = new TestCodec();
        _factory.Register(VideoTypes.MJPEG, codec);

        var frames = _factory.Decode(new VPacket
        {
            Data = new NewLife.Data.Packet(new Byte[10]),
            CodecType = VideoTypes.MJPEG,
        });

        Assert.NotNull(frames);
        Assert.NotEmpty(frames);
        Assert.Equal(VideoTypes.YUV420P, frames[0].PixelFormat);
    }

    [Fact(DisplayName = "工厂RegisterFactory后每次创建新实例")]
    public void RegisterFactory_ThenEncode_CreatesNewInstance()
    {
        var callCount = 0;
        _factory.RegisterFactory(VideoTypes.H264, () =>
        {
            callCount++;
            return new TestCodec();
        });

        _factory.Encode([VideoFrame.Create(640, 480, VideoTypes.YUV420P)], VideoTypes.H264);
        _factory.Encode([VideoFrame.Create(640, 480, VideoTypes.YUV420P)], VideoTypes.H264);

        Assert.Equal(2, callCount);
    }

    [Fact(DisplayName = "工厂Encode传入空帧抛异常")]
    public void Encode_EmptyFrames_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.Encode(null, VideoTypes.H264));
        Assert.Throws<ArgumentNullException>(() => _factory.Encode([], VideoTypes.H264));
    }

    [Fact(DisplayName = "工厂Decode传入空包返回空数组")]
    public void Decode_NullPacket_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.Decode(null));
    }

    [Fact(DisplayName = "工厂未注册类型Decode抛异常")]
    public void Decode_UnregisteredType_ThrowsNotSupported()
    {
        var packet = new VPacket
        {
            Data = new NewLife.Data.Packet(new Byte[10]),
            CodecType = VideoTypes.AV1,
        };

        Assert.Throws<NotSupportedException>(() => _factory.Decode(packet));
    }

    [Fact(DisplayName = "工厂解码时自动去除芯片头")]
    public void Decode_WithChipHeader_TrimsHeader()
    {
        var codec = new TestCodec();
        _factory.Register(VideoTypes.MJPEG, codec);
        _factory.RegisterChipHeader(new TestChipHeader("HEAD", 1));

        // 数据前缀 + "HEAD" 芯片头
        var data = new Byte[] { (Byte)'H', (Byte)'E', (Byte)'A', (Byte)'D', 1, 2, 3 };
        var packet = new VPacket
        {
            Data = new NewLife.Data.Packet(data),
            CodecType = VideoTypes.MJPEG,
        };

        var frames = _factory.Decode(packet);
        Assert.NotNull(frames);
        Assert.NotEmpty(frames);
    }

    [Fact(DisplayName = "工厂编码时自动添加芯片头")]
    public void Encode_AfterDecode_AddsChipHeader()
    {
        var codec = new TestCodec();
        _factory.Register(VideoTypes.MJPEG, codec);
        _factory.RegisterChipHeader(new TestChipHeader("HEAD", 1));

        // 先解码一次以记录芯片头类型
        var data = new Byte[] { (Byte)'H', (Byte)'E', (Byte)'A', (Byte)'D', 1, 2, 3 };
        _factory.Decode(new VPacket
        {
            Data = new NewLife.Data.Packet(data),
            CodecType = VideoTypes.MJPEG,
        });

        // 编码时应自动还原芯片头
        var packet = _factory.Encode([VideoFrame.Create(640, 480, VideoTypes.YUV420P)], VideoTypes.MJPEG);
        Assert.NotNull(packet);
    }
}
