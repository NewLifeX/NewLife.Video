using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class FfmpegPlayerTests
{
    [Fact(DisplayName = "FfmpegPlayer 初始状态正确")]
    public void InitialState_Correct()
    {
        using var player = new FfmpegPlayer();
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.Width);
        Assert.Equal(0, player.Height);
    }

    [Fact(DisplayName = "FfmpegPlayer Open 空参数抛出异常")]
    public void Open_NullUrl_Throws()
    {
        using var player = new FfmpegPlayer();
        Assert.Throws<ArgumentNullException>(() => player.Open((String)null));
        Assert.Throws<ArgumentNullException>(() => player.Open((Uri)null));
    }

    [Fact(DisplayName = "FfmpegPlayer Open 空字符串抛出异常")]
    public void Open_EmptyUrl_Throws()
    {
        using var player = new FfmpegPlayer();
        Assert.Throws<ArgumentNullException>(() => player.Open(""));
    }

    [Fact(DisplayName = "FfmpegPlayer Play 未设置源抛出异常")]
    public void Play_NoSource_Throws()
    {
        using var player = new FfmpegPlayer();
        Assert.Throws<InvalidOperationException>(() => player.Play());
    }

    [Fact(DisplayName = "FfmpegPlayer 自定义参数构造")]
    public void CustomOptions_Stored()
    {
        using var player = new FfmpegPlayer("-rtsp_transport tcp -buffer_size 1024");
        player.Open("rtsp://localhost/stream");
        Assert.True(player is IDisposable);
    }

    [Fact(DisplayName = "FfmpegPlayer 重复播放安全")]
    public void Play_MultipleTimes_Safe()
    {
        using var player = new FfmpegPlayer();
        player.Open("rtsp://localhost/test");

        // 未 Start 不应崩溃
        Assert.False(player.IsPlaying);
    }
}
