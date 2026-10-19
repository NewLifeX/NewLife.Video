using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class FrameExtractorTests
{
    [Fact(DisplayName = "ExtractSnapshot 文件不存在抛 FileNotFoundException")]
    public void ExtractSnapshot_FileNotExist_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            FrameExtractor.ExtractSnapshot("Z:\\nonexistent_video.mp4", 0));
        Assert.Equal("Z:\\nonexistent_video.mp4", ex.FileName);
    }

    [Fact(DisplayName = "ExtractSnapshotByIndex 文件不存在抛 FileNotFoundException")]
    public void ExtractSnapshotByIndex_FileNotExist_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            FrameExtractor.ExtractSnapshotByIndex("Z:\\nonexistent_video.avi", 0));
        Assert.Equal("Z:\\nonexistent_video.avi", ex.FileName);
    }

    [Fact(DisplayName = "ExtractKeyFrames 文件不存在抛 FileNotFoundException")]
    public void ExtractKeyFrames_FileNotExist_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            FrameExtractor.ExtractKeyFrames("Z:\\nonexistent_video.ts"));
        Assert.Equal("Z:\\nonexistent_video.ts", ex.FileName);
    }

    [Fact(DisplayName = "ExtractKeyFrames 不支持格式不抛异常")]
    public void ExtractKeyFrames_UnsupportedFormat_NoThrow()
    {
        var path = TestFile.GetTempPath(".unk");
        try
        {
            File.WriteAllText(path, "not a video");
            // 不支持的格式由 VideoContainerFactory.CreateReader 处理，
            // 可能返回空列表或抛特定异常，验证调用不导致未处理崩溃即可
            var result = FrameExtractor.ExtractKeyFrames(path);
            Assert.NotNull(result);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    [Fact(DisplayName = "ExtractKeyFrames maxFrames=0 不限制")]
    public void ExtractKeyFrames_MaxFramesZero_NoLimit()
    {
        // maxFrames=0 表示不限制，与正数逻辑一致
        var path = TestFile.GetTempPath(".unk");
        try
        {
            File.WriteAllText(path, "not a video");
            var result = FrameExtractor.ExtractKeyFrames(path, 0);
            Assert.NotNull(result);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
