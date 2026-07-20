using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class Fmp4WebSocketStreamerTests
{
    [Fact(DisplayName = "Fmp4WebSocketStreamer 初始状态")]
    public void InitialState_Correct()
    {
        using var streamer = new Fmp4WebSocketStreamer(9090);
        Assert.False(streamer.IsRunning);
        Assert.Equal(0, streamer.ClientCount);
    }

    [Fact(DisplayName = "Fmp4WebSocketStreamer Start/Stop 安全")]
    public void StartStop_Safe()
    {
        using var streamer = new Fmp4WebSocketStreamer(9091);
        streamer.Start();
        Assert.True(streamer.IsRunning);

        streamer.Stop();
        Assert.False(streamer.IsRunning);
    }

    [Fact(DisplayName = "Fmp4WebSocketStreamer 重复 Stop 安全")]
    public void DoubleStop_Safe()
    {
        using var streamer = new Fmp4WebSocketStreamer(9092);
        streamer.Stop();
        streamer.Stop();
        Assert.False(streamer.IsRunning);
    }

    [Fact(DisplayName = "Fmp4WebSocketStreamer Dispose 安全")]
    public void Dispose_Safe()
    {
        var streamer = new Fmp4WebSocketStreamer(9093);
        streamer.Start();
        streamer.Dispose();
        Assert.False(streamer.IsRunning);
    }

    [Fact(DisplayName = "Fmp4WebSocketStreamer PushFrame 空参数安全")]
    public void PushFrame_Null_Safe()
    {
        using var streamer = new Fmp4WebSocketStreamer(9094);
        streamer.PushFrame(null);
        Assert.False(streamer.IsRunning);
    }
}
