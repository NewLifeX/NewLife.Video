using NewLife.Video;
using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest;

public class VideoContainerFactoryTests
{
    [Fact(DisplayName = "工厂创建不存在的文件抛异常")]
    public void CreateReader_NonExistentFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            VideoContainerFactory.CreateReader("Z:\\nonexistent\\file.yuv"));
    }

    [Fact(DisplayName = "工厂按.yuv扩展名创建RawVideoReader")]
    public void CreateReader_YuvExtension_CreatesRawReader()
    {
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            // 创建一个小的测试文件
            var data = new Byte[640 * 480 * 3 / 2]; // 一个 YUV420P 帧
            File.WriteAllBytes(path, data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<RawVideoReader>(reader);
            Assert.True(reader.VideoWidth > 0);
            Assert.True(reader.VideoHeight > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "工厂按.nv12扩展名创建RawVideoReader")]
    public void CreateReader_Nv12Extension_CreatesRawReader()
    {
        var path = Path.GetTempFileName() + ".nv12";
        try
        {
            var data = new Byte[640 * 480 * 3 / 2];
            File.WriteAllBytes(path, data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<RawVideoReader>(reader);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "工厂按.mp4扩展名创建Mp4Reader")]
    public void CreateReader_Mp4Extension_CreatesMp4Reader()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            // 创建最小 MP4
            var mp4Data = CreateMinimalMp4Bytes();
            File.WriteAllBytes(path, mp4Data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<NewLife.Video.Containers.Mp4Reader>(reader);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>创建最小 MP4 用于测试工厂路由</summary>
    private static Byte[] CreateMinimalMp4Bytes()
    {
        using var ms = new MemoryStream();
        // ftyp
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00, 0x69, 0x73, 0x6F, 0x6D });
        // moov (最小)
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x08, 0x6D, 0x6F, 0x6F, 0x76 });
        // mdat (空)
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x08, 0x6D, 0x64, 0x61, 0x74 });
        return ms.ToArray();
    }
}
