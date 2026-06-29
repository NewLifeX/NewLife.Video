using NewLife.Data;
using NewLife.Video;
using NewLife.Video.ChipHeaders;
using Xunit;

namespace XUnitTest.ChipHeaders;

public class VideoChipHeaderTests
{
    [Fact(DisplayName = "海思视频头TryTrim正确")]
    public void Hisilicon_TryTrim_Correct()
    {
        var header = new HisiliconVideoHeader();
        var data = new ArrayPacket(new Byte[]
        {
            (Byte)'H', (Byte)'I', (Byte)'S', (Byte)'I', // magic
            0x00, 0x00, 0x00, 0x05, // length = 5
            0x01, 0x02, 0x03, 0x04, 0x05, // payload
        });

        var result = header.TryTrim(data, out var trimmed);
        Assert.True(result);
        Assert.NotNull(trimmed);
        Assert.Equal(5, trimmed.Total);
        Assert.Equal(0x01, trimmed[0]);
    }

    [Fact(DisplayName = "海思视频头非海思数据返回false")]
    public void Hisilicon_NonHisilicon_False()
    {
        var header = new HisiliconVideoHeader();
        var data = new ArrayPacket(new Byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        Assert.False(header.TryTrim(data, out _));
    }

    [Fact(DisplayName = "海思视频头TryAdd往返")]
    public void Hisilicon_TryAdd_RoundTrip()
    {
        var header = new HisiliconVideoHeader();
        var original = new ArrayPacket(new Byte[] { 0xAA, 0xBB, 0xCC });
        Assert.True(header.TryAdd(original, out var withHeader));
        Assert.True(header.TryTrim(withHeader, out var restored));
        Assert.Equal(original.Total, restored.Total);
        for (var i = 0; i < original.Total; i++)
            Assert.Equal(original[i], restored[i]);
    }

    [Fact(DisplayName = "大华视频头TryTrim正确")]
    public void Dahua_TryTrim_Correct()
    {
        var header = new DahuaVideoHeader();
        var data = new ArrayPacket(new Byte[]
        {
            0xDA, 0x41, 0x48, 0x00, 0x00, 0x01, // 6-byte header
            0x10, 0x20, // payload
        });
        Assert.True(header.TryTrim(data, out var trimmed));
        Assert.Equal(2, trimmed.Total);
    }

    [Fact(DisplayName = "大华视频头TryAdd往返")]
    public void Dahua_TryAdd_RoundTrip()
    {
        var header = new DahuaVideoHeader();
        var original = new ArrayPacket(new Byte[] { 0x11, 0x22 });
        Assert.True(header.TryAdd(original, out var withHeader));
        Assert.True(header.TryTrim(withHeader, out var restored));
        Assert.Equal(original.Total, restored.Total);
    }

    [Fact(DisplayName = "宇视视频头TryTrim正确")]
    public void Uniview_TryTrim_Correct()
    {
        var header = new UniviewVideoHeader();
        var data = new ArrayPacket(new Byte[] { (Byte)'U', (Byte)'N', (Byte)'V', 0x00, 0x01, 0x02 });
        Assert.True(header.TryTrim(data, out var trimmed));
        Assert.Equal(2, trimmed.Total);
    }

    [Fact(DisplayName = "宇视视频头TryAdd往返")]
    public void Uniview_TryAdd_RoundTrip()
    {
        var header = new UniviewVideoHeader();
        var original = new ArrayPacket(new Byte[] { 0x30, 0x31 });
        Assert.True(header.TryAdd(original, out var withHeader));
        Assert.True(header.TryTrim(withHeader, out var restored));
        Assert.Equal(original.Total, restored.Total);
    }

    [Fact(DisplayName = "芯片头优先级正确")]
    public void Priority_Correct()
    {
        Assert.Equal(1, new HisiliconVideoHeader().Priority);
        Assert.Equal(2, new DahuaVideoHeader().Priority);
        Assert.Equal(3, new UniviewVideoHeader().Priority);
    }

    [Fact(DisplayName = "VideoCodecFactory注册芯片头后自动去除")]
    public void Factory_WithChipHeaders_TrimsCorrectly()
    {
        var factory = new VideoCodecFactory();
        factory.RegisterChipHeader(new HisiliconVideoHeader());
        factory.RegisterChipHeader(new DahuaVideoHeader());

        // 注册一个测试编解码器
        var codec = new XUnitTest.TestCodec();
        factory.Register(VideoTypes.MJPEG, codec);

        // 构造海思头数据
        var payload = new Byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }; // minimal JPEG
        var headerLen = payload.Length;
        var hisiData = new Byte[8 + payload.Length];
        hisiData[0] = (Byte)'H'; hisiData[1] = (Byte)'I'; hisiData[2] = (Byte)'S'; hisiData[3] = (Byte)'I';
        hisiData[4] = (Byte)(headerLen >> 24); hisiData[5] = (Byte)(headerLen >> 16);
        hisiData[6] = (Byte)(headerLen >> 8); hisiData[7] = (Byte)(headerLen);
        Array.Copy(payload, 0, hisiData, 8, payload.Length);

        var packet = new VPacket { Data = new ArrayPacket(hisiData), CodecType = VideoTypes.MJPEG };
        var frames = factory.Decode(packet);
        Assert.NotEmpty(frames);
    }
}
