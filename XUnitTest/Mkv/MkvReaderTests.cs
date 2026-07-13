using NewLife.Video.Containers;
using NewLife.Video.Mkv;
using Xunit;

namespace XUnitTest.Mkv;

public class MkvReaderTests
{
    [Fact(DisplayName = "MkvReader 不存在的文件抛异常")]
    public void NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new MkvReader("Z:\\nope.mkv"));
    }

    [Fact(DisplayName = "MkvReader 工厂路由.mkv")]
    public void Factory_Routes()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            // EBML 头: 0x1A 0x45 0xDF 0xA3
            var data = new Byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20 };
            File.WriteAllBytes(path, data);
            using var r = VideoContainerFactory.CreateReader(path);
            Assert.IsType<MkvReader>(r);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvReader 无效EBML返回空帧")]
    public void InvalidEbml_NoFrames()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            File.WriteAllBytes(path, new Byte[] { 0x00, 0x00, 0x00, 0x00 });
            using var r = new MkvReader(path);
            Assert.Equal(-1, r.TotalFrames); // 无效文件无帧
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvReader 不存在文件抛FileNotFoundException")]
    public void MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            using var r = new MkvReader("nonexistent_" + Guid.NewGuid() + ".mkv");
        });
    }
}
