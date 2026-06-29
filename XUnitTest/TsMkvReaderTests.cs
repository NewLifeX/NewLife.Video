using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest;

public class TsMkvReaderTests
{
    [Fact(DisplayName = "TsReader 不存在的文件抛异常")]
    public void TsReader_NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new TsReader("Z:\\nope.ts"));
    }

    [Fact(DisplayName = "TsReader 工厂路由.ts")]
    public void TsReader_Factory_Routes()
    {
        var path = Path.GetTempFileName() + ".ts";
        try
        {
            // 最小 TS 文件（空同步字节序列）
            var data = new Byte[188];
            data[0] = 0x47;
            File.WriteAllBytes(path, data);
            using var r = NewLife.Video.VideoContainerFactory.CreateReader(path);
            Assert.IsType<TsReader>(r);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvReader 不存在的文件抛异常")]
    public void MkvReader_NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new MkvReader("Z:\\nope.mkv"));
    }

    [Fact(DisplayName = "MkvReader 工厂路由.mkv")]
    public void MkvReader_Factory_Routes()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            // EBML 头: 0x1A 0x45 0xDF 0xA3
            var data = new Byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20 };
            File.WriteAllBytes(path, data);
            using var r = NewLife.Video.VideoContainerFactory.CreateReader(path);
            Assert.IsType<MkvReader>(r);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "MkvReader 无效EBML抛异常")]
    public void MkvReader_InvalidEbml_Throws()
    {
        var path = Path.GetTempFileName() + ".mkv";
        try
        {
            File.WriteAllBytes(path, new Byte[] { 0x00, 0x00, 0x00, 0x00 });
            Assert.Throws<InvalidOperationException>(() => new MkvReader(path));
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "TsReader 读取空文件不抛异常")]
    public void TsReader_EmptyFile_NoThrow()
    {
        var path = Path.GetTempFileName() + ".ts";
        try
        {
            var data = new Byte[188];
            data[0] = 0x47;
            File.WriteAllBytes(path, data);
            using var r = new TsReader(path);
            Assert.NotNull(r);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }
}
