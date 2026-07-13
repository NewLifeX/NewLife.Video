using NewLife.Video.Avi;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest.Avi;

public class AviReaderTests
{
    [Fact(DisplayName = "AviReader不存在的文件抛异常")]
    public void Constructor_NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => new AviReader("Z:\\nope.avi"));
    }

    [Fact(DisplayName = "AviReader工厂路由.avi")]
    public void Factory_RoutesAvi()
    {
        var path = Path.GetTempFileName() + ".avi";
        try
        {
            // 创建包含 RIFF 魔术字节的最小文件
            var riff = new Byte[24];
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(riff, 0);
            riff[4] = 16; riff[5] = 0; riff[6] = 0; riff[7] = 0; // size=16
            System.Text.Encoding.ASCII.GetBytes("AVI ").CopyTo(riff, 8);
            System.Text.Encoding.ASCII.GetBytes("LIST").CopyTo(riff, 12);
            riff[16] = 4; // list size=4
            System.Text.Encoding.ASCII.GetBytes("movi").CopyTo(riff, 20);

            File.WriteAllBytes(path, riff);
            using var reader = VideoContainerFactory.CreateReader(path);
            Assert.IsType<AviReader>(reader);
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "AviReader无效RIFF抛异常")]
    public void Constructor_InvalidRiff_Throws()
    {
        var path = Path.GetTempFileName() + ".avi";
        try
        {
            File.WriteAllBytes(path, new Byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            Assert.Throws<InvalidOperationException>(() => new AviReader(path));
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }
}

