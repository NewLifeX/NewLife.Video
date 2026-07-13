using NewLife.Video;
using NewLife.Video.Codecs;
using Xunit;

namespace XUnitTest.Codecs;

public class VideoFormatDetectorTests
{
    [Fact(DisplayName = "检测JPEG SOI标记")]
    public void Detect_JPEG_ReturnsMjpeg()
    {
        var data = new Byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        Assert.Equal(VideoTypes.MJPEG, VideoFormatDetector.Detect(data));
    }

    [Fact(DisplayName = "检测H.264 AnnexB起始码")]
    public void Detect_H264AnnexB_ReturnsH264()
    {
        // SPS NAL: 起始码 0x00 0x00 0x00 0x01, NAL type=7
        var data = new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x1E };
        Assert.Equal(VideoTypes.H264, VideoFormatDetector.Detect(data));
    }

    [Fact(DisplayName = "检测H.264 3字节起始码")]
    public void Detect_H264ShortStart_ReturnsH264()
    {
        var data = new Byte[] { 0x00, 0x00, 0x01, 0x65, 0x88, 0x84 };
        Assert.Equal(VideoTypes.H264, VideoFormatDetector.Detect(data));
    }

    [Fact(DisplayName = "检测H.265 NAL头")]
    public void Detect_H265_ReturnsH265()
    {
        // VPS: 0x00 0x00 0x00 0x01, NAL header 0x40 0x01 (type=32)
        var data = new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x40, 0x01, 0x0C, 0x01 };
        Assert.Equal(VideoTypes.H265, VideoFormatDetector.Detect(data));
    }

    [Fact(DisplayName = "检测未知数据返回Unknown")]
    public void Detect_Random_ReturnsUnknown()
    {
        var data = new Byte[] { 0xAB, 0xCD, 0xEF, 0x00, 0x11, 0x22 };
        Assert.Equal(VideoTypes.Unknown, VideoFormatDetector.Detect(data));
    }

    [Fact(DisplayName = "便捷检测方法正确")]
    public void ConvenienceMethods_Correct()
    {
        Assert.True(VideoFormatDetector.IsH264(new Byte[] { 0x00, 0x00, 0x00, 0x01, 0x67 }));
        Assert.True(VideoFormatDetector.IsMjpeg(new Byte[] { 0xFF, 0xD8 }));
        Assert.False(VideoFormatDetector.IsH265(new Byte[] { 0xAB, 0xCD }));
    }

    [Fact(DisplayName = "空数据检测返回Unknown")]
    public void Detect_Empty_ReturnsUnknown()
    {
        Assert.Equal(VideoTypes.Unknown, VideoFormatDetector.Detect(null));
        Assert.Equal(VideoTypes.Unknown, VideoFormatDetector.Detect(new Byte[1]));
    }
}
