using NewLife.Video;
using Xunit;

namespace XUnitTest;

public class VideoTypesTests
{
    [Fact(DisplayName = "VideoTypes枚举值正确映射")]
    public void EnumValues_CorrectMapping()
    {
        Assert.Equal(98, (Byte)VideoTypes.H264);
        Assert.Equal(99, (Byte)VideoTypes.H265);
        Assert.Equal(1, (Byte)VideoTypes.MJPEG);
        Assert.Equal(50, (Byte)VideoTypes.YUV420P);
        Assert.Equal(51, (Byte)VideoTypes.NV12);
        Assert.Equal(70, (Byte)VideoTypes.RGB24);
    }

    [Fact(DisplayName = "VideoTypes编码类型与像素格式区分")]
    public void EnumValues_CodecVsPixel()
    {
        // 编码类型范围 1~99（除 50~80 像素格式）
        var codecTypes = new[] { VideoTypes.H264, VideoTypes.H265, VideoTypes.MJPEG, VideoTypes.VP8, VideoTypes.VP9, VideoTypes.AV1 };
        foreach (var ct in codecTypes)
        {
            Assert.True((Byte)ct < 50 || (Byte)ct > 80);
        }

        // 像素格式范围 50~80
        var pixelFormats = new[] { VideoTypes.YUV420P, VideoTypes.NV12, VideoTypes.RGB24, VideoTypes.GRAY8 };
        foreach (var pf in pixelFormats)
        {
            Assert.True((Byte)pf >= 50 && (Byte)pf <= 85);
        }
    }

    [Fact(DisplayName = "VideoTypes所有枚举值唯一")]
    public void AllEnumValues_AreUnique()
    {
        var values = Enum.GetValues(typeof(VideoTypes)).Cast<Byte>().ToList();
        var distinct = values.Distinct().ToList();
        Assert.Equal(distinct.Count, values.Count);
    }
}
