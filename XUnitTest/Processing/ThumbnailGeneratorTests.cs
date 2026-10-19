using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class ThumbnailGeneratorTests
{
    [Fact(DisplayName = "ThumbnailGenerator.GenerateGrid 文件不存在抛 FileNotFoundException")]
    public void GenerateGrid_FileNotExist_Throws()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            ThumbnailGenerator.GenerateGrid("Z:\\nonexistent.mp4"));
        Assert.Equal("Z:\\nonexistent.mp4", ex.FileName);
    }

    [Fact(DisplayName = "ThumbnailGenerator.GenerateGrid 自定义行列参数")]
    public void GenerateGrid_CustomColsRows_FileNotExistThrows()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            ThumbnailGenerator.GenerateGrid("Z:\\nonexistent.mp4", cols: 2, rows: 2));
        Assert.Equal("Z:\\nonexistent.mp4", ex.FileName);
    }

    [Fact(DisplayName = "ThumbnailGenerator.GenerateGrid 自定义缩略图尺寸")]
    public void GenerateGrid_CustomThumbSize_FileNotExistThrows()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            ThumbnailGenerator.GenerateGrid("Z:\\nonexistent.mp4", 3, 3, 200, 120));
        Assert.Equal("Z:\\nonexistent.mp4", ex.FileName);
    }

    [Fact(DisplayName = "ThumbnailGenerator.GenerateSingle 文件不存在抛 FileNotFoundException")]
    public void GenerateSingle_FileNotExist_Throws()
    {
        // GenerateSingle 内部调用 VideoContainerFactory.CreateReader，文件不存在时可能先抛 FileNotFoundException
        Assert.Throws<FileNotFoundException>(() =>
            ThumbnailGenerator.GenerateSingle("Z:\\nonexistent.avi"));
    }

    [Fact(DisplayName = "ThumbnailGenerator.GenerateSingle 自定义尺寸参数")]
    public void GenerateSingle_CustomSize_FileNotExistThrows()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ThumbnailGenerator.GenerateSingle("Z:\\nonexistent.mp4", 640, 360));
    }
}
