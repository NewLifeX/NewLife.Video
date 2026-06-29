using System;
using NewLife.Video;
using Xunit;

namespace XUnitTest;

public class CodecRegistryTests
{
    private readonly CodecRegistry _registry = new();

    [Fact(DisplayName = "注册表Register后GetCodec返回同一实例")]
    public void Register_ThenGetCodec_ReturnsSameInstance()
    {
        var codec = new TestCodec();
        _registry.Register(VideoTypes.MJPEG, codec);

        var result = _registry.GetCodec(VideoTypes.MJPEG);
        Assert.Same(codec, result);
    }

    [Fact(DisplayName = "注册表RegisterFactory每次GetCodec返回新实例")]
    public void RegisterFactory_ThenGetCodec_ReturnsNewInstance()
    {
        _registry.RegisterFactory(VideoTypes.H264, () => new TestCodec());

        var codec1 = _registry.GetCodec(VideoTypes.H264);
        var codec2 = _registry.GetCodec(VideoTypes.H264);

        Assert.NotSame(codec1, codec2);
    }

    [Fact(DisplayName = "注册表未注册类型GetCodec抛异常")]
    public void GetCodec_UnregisteredType_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => _registry.GetCodec(VideoTypes.H265));
    }

    [Fact(DisplayName = "注册表IsSupported正确反映注册状态")]
    public void IsSupported_ReflectsRegistration()
    {
        Assert.False(_registry.IsSupported(VideoTypes.MJPEG));

        _registry.Register(VideoTypes.MJPEG, new TestCodec());
        Assert.True(_registry.IsSupported(VideoTypes.MJPEG));
        Assert.False(_registry.IsSupported(VideoTypes.H264));
    }

    [Fact(DisplayName = "注册表RegisteredTypes返回所有已注册类型")]
    public void RegisteredTypes_ReturnsAllRegistered()
    {
        _registry.Register(VideoTypes.MJPEG, new TestCodec());
        _registry.Register(VideoTypes.VP8, new TestCodec());
        _registry.RegisterFactory(VideoTypes.H264, () => new TestCodec());

        var types = _registry.RegisteredTypes;
        Assert.Contains(VideoTypes.MJPEG, types);
        Assert.Contains(VideoTypes.VP8, types);
        Assert.Contains(VideoTypes.H264, types);
        Assert.Equal(3, types.Count);
    }

    [Fact(DisplayName = "注册表Register传null抛异常")]
    public void Register_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Register(VideoTypes.MJPEG, null));
        Assert.Throws<ArgumentNullException>(() => _registry.RegisterFactory(VideoTypes.MJPEG, null));
    }
}
