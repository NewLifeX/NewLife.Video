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

    [Fact(DisplayName = "工厂对未实现格式抛NotSupported")]
    public void CreateReader_UnsupportedFormat_ThrowsNotSupported()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            File.WriteAllBytes(path, new Byte[100]);

            Assert.Throws<NotSupportedException>(() =>
                VideoContainerFactory.CreateReader(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
