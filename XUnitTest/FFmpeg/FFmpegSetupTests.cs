using NewLife.Video.FFmpeg;
using Xunit;

namespace XUnitTest.FFmpeg;

public class FFmpegSetupTests
{
    [Fact(DisplayName = "FFmpegSetup 默认属性非空")]
    public void DefaultProperties_NotNull()
    {
        Assert.NotNull(FFmpegSetup.DownloadBaseUrl);
        Assert.NotNull(FFmpegSetup.FFmpegDirectory);
        Assert.NotNull(FFmpegSetup.CacheDirectory);
    }

    [Fact(DisplayName = "FFmpegSetup 自定义下载源")]
    public void CustomDownloadUrl()
    {
        var original = FFmpegSetup.DownloadBaseUrl;
        try
        {
            FFmpegSetup.DownloadBaseUrl = "https://example.com/plugins";
            Assert.Equal("https://example.com/plugins", FFmpegSetup.DownloadBaseUrl);
        }
        finally
        {
            FFmpegSetup.DownloadBaseUrl = original;
        }
    }

    [Fact(DisplayName = "FFmpegSetup 自定义目录")]
    public void CustomDirectory()
    {
        var originalDir = FFmpegSetup.FFmpegDirectory;
        var originalCache = FFmpegSetup.CacheDirectory;
        try
        {
            FFmpegSetup.FFmpegDirectory = "D:\\custom\\ffmpeg";
            Assert.Equal("D:\\custom\\ffmpeg", FFmpegSetup.FFmpegDirectory);

            FFmpegSetup.CacheDirectory = "D:\\custom\\cache";
            Assert.Equal("D:\\custom\\cache", FFmpegSetup.CacheDirectory);
        }
        finally
        {
            FFmpegSetup.FFmpegDirectory = originalDir;
            FFmpegSetup.CacheDirectory = originalCache;
        }
    }

    [Fact(DisplayName = "FFmpegSetup IsAvailable 不崩溃")]
    public void IsAvailable_NoCrash()
    {
        // 应安全返回，不抛出异常
        var available = FFmpegSetup.IsAvailable();
        // 结果取决于系统是否安装 ffmpeg，仅确保不崩溃
        Assert.True(true);
    }
}
