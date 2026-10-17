using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class SnapshotEncoderTests
{
    [Fact(DisplayName = "SnapshotEncoder BMP编码正确")]
    public void EncodeToBmp_ProducesValidBmp()
    {
        var frame = VideoFrame.Create(64, 48, VideoTypes.RGB24);

        var bmp = SnapshotEncoder.EncodeToBmp(frame);

        Assert.NotNull(bmp);
        Assert.True(bmp.Length > 54); // BMP header minimum
        Assert.Equal(0x42, bmp[0]);  // 'B'
        Assert.Equal(0x4D, bmp[1]);  // 'M'
    }

    [Fact(DisplayName = "SnapshotEncoder BMP文件大小正确")]
    public void EncodeToBmp_CorrectFileSize()
    {
        var width = 32;
        var height = 32;
        var frame = VideoFrame.Create(width, height, VideoTypes.RGB24);

        var bmp = SnapshotEncoder.EncodeToBmp(frame);

        var rowSize = (width * 3 + 3) & ~3;
        var expectedSize = 54 + rowSize * height;
        Assert.Equal(expectedSize, bmp.Length);
    }

    [Fact(DisplayName = "SnapshotEncoder null帧统一抛异常")]
    public void Encode_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.EncodeToJpeg(null));
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.EncodeToPng(null));
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.EncodeToWebP(null));
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.EncodeToBmp(null));
    }

    [Fact(DisplayName = "SnapshotEncoder SaveSnapshot空路径抛异常")]
    public void SaveSnapshot_NullPath_Throws()
    {
        var frame = VideoFrame.Create(16, 16, VideoTypes.RGB24);
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.SaveSnapshot(frame, null));
        Assert.Throws<ArgumentNullException>(() => SnapshotEncoder.SaveSnapshot(frame, ""));
    }

    [Fact(DisplayName = "SnapshotEncoder 根据扩展名推断格式")]
    public void SaveSnapshot_InferFormat()
    {
        var frame = VideoFrame.Create(16, 16, VideoTypes.RGB24);

        // BMP 不需要 ffmpeg，可以直接验证
        var path = TestFile.GetTempPath(".bmp");
        try
        {
            SnapshotEncoder.SaveSnapshot(frame, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }
}
