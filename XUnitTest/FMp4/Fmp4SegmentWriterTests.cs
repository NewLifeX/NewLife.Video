using NewLife.Data;
using NewLife.Video;
using NewLife.Video.FMp4;
using Xunit;

namespace XUnitTest.FMp4;

public class Fmp4SegmentWriterTests
{
    [Fact(DisplayName = "Fmp4SegmentWriter 创建文件写入器")]
    public void Create_FileWriter_Success()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var writer = new Fmp4SegmentWriter(path);
            Assert.NotNull(writer);
            Assert.Equal(0, writer.SegmentCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "Fmp4SegmentWriter 空路径抛出异常")]
    public void Create_NullPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Fmp4SegmentWriter((String)null));
        Assert.Throws<ArgumentNullException>(() => new Fmp4SegmentWriter((Stream)null));
    }

    [Fact(DisplayName = "Fmp4SegmentWriter WriteMetadata 后写入帧")]
    public void WriteFrame_AfterMetadata_Success()
    {
        using var ms = new MemoryStream();
        using var writer = new Fmp4SegmentWriter(ms, segmentFrames: 30, leaveOpen: true);

        writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);

        var packet = new VPacket(new ArrayPacket(new Byte[] { 0, 0, 0, 1, 0x67, 0x42, 0x00, 0x1e, 0xa7 }), VideoTypes.H264)
        {
            IsKeyFrame = true,
            PTS = 0,
        };
        writer.WriteFrame(packet);

        Assert.True(ms.Length > 0);
    }

    [Fact(DisplayName = "Fmp4SegmentWriter 先 WriteFrame 再 WriteMetadata 抛出异常")]
    public void WriteFrame_BeforeMetadata_Throws()
    {
        using var ms = new MemoryStream();
        using var writer = new Fmp4SegmentWriter(ms, leaveOpen: true);

        var packet = new VPacket(new ArrayPacket(new Byte[] { 1, 2, 3 }), VideoTypes.Unknown);
        Assert.Throws<InvalidOperationException>(() => { writer.WriteFrame(packet); });
    }

    [Fact(DisplayName = "Fmp4SegmentWriter 空帧写入安全")]
    public void WriteFrame_Null_Safe()
    {
        using var ms = new MemoryStream();
        using var writer = new Fmp4SegmentWriter(ms, leaveOpen: true);
        writer.WriteMetadata(640, 480, VideoTypes.H264, 25);
        writer.WriteFrame(null);
        writer.WriteFrame(new VPacket((IPacket)null, VideoTypes.Unknown));
        Assert.True(true);
    }
}
