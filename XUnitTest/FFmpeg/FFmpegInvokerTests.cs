using NewLife.Video.FFmpeg;
using Xunit;

namespace XUnitTest.FFmpeg;

public class FFmpegInvokerTests
{
    [Fact(DisplayName = "FFmpegInvoker自动查找路径")]
    public void Constructor_AutoFind_SetsPath()
    {
        var invoker = new FFmpegInvoker();

        // 不抛异常即通过（路径可能不存在，但不影响构造）
        Assert.NotNull(invoker.FFmpegPath);
        Assert.NotNull(invoker.FFprobePath);
    }

    [Fact(DisplayName = "FFmpegInvoker可设置自定义路径")]
    public void Constructor_CustomPath_SetsCustom()
    {
        var invoker = new FFmpegInvoker(@"C:\custom\ffmpeg.exe");

        Assert.Equal(@"C:\custom\ffmpeg.exe", invoker.FFmpegPath);
    }

    [Fact(DisplayName = "FFmpegInvoker超时参数可设置")]
    public void TimeoutMs_CanBeSet()
    {
        var invoker = new FFmpegInvoker { TimeoutMs = 5000 };

        Assert.Equal(5000, invoker.TimeoutMs);
    }

    [Fact(DisplayName = "FFmpegInvoker工作目录可设置")]
    public void WorkingDirectory_CanBeSet()
    {
        var invoker = new FFmpegInvoker { WorkingDirectory = @"C:\temp" };

        Assert.Equal(@"C:\temp", invoker.WorkingDirectory);
    }

    [Fact(DisplayName = "FFmpegResult成功状态")]
    public void FFmpegResult_Success_Correct()
    {
        var result = new FFmpegResult { ExitCode = 0, Output = "ok" };

        Assert.True(result.Success);
        Assert.Equal("ok", result.Output);
    }

    [Fact(DisplayName = "FFmpegResult失败状态")]
    public void FFmpegResult_Failure_Correct()
    {
        var result = new FFmpegResult { ExitCode = 1, Error = "error" };

        Assert.False(result.Success);
        Assert.Equal("error", result.Error);
    }
}
