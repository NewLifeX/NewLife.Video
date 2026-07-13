using NewLife.Video.Containers;
using NewLife.Video.Ts;
using Xunit;

namespace XUnitTest.Ts;

public class TsReaderTests
{
    [Fact(DisplayName = "TsReader 不存在的文件抛异常")]
    public void NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new TsReader("Z:\\nope.ts"));
    }

    [Fact(DisplayName = "TsReader 工厂路由.ts")]
    public void Factory_Routes()
    {
        var path = Path.GetTempFileName() + ".ts";
        try
        {
            // 最小 TS 文件（空同步字节序列）
            var data = new Byte[188];
            data[0] = 0x47;
            File.WriteAllBytes(path, data);
            using var r = VideoContainerFactory.CreateReader(path);
            Assert.IsType<TsReader>(r);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "TsReader 读取空文件不抛异常")]
    public void EmptyFile_NoThrow()
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
