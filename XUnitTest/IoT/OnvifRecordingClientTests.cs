using System.Threading.Tasks;
using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class OnvifRecordingClientTests
{
    [Fact(DisplayName = "OnvifRecordingClient 空服务地址抛出异常")]
    public void Create_NullUrl_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OnvifRecordingClient(null));
    }

    [Fact(DisplayName = "OnvifRecordingClient 构造")]
    public void Create_WithUrl_Success()
    {
        using var client = new OnvifRecordingClient("http://192.168.1.64/onvif/recording", "admin", "12345");
        Assert.NotNull(client);
    }

    [Fact(DisplayName = "OnvifRecordingClient Dispose 安全")]
    public void Dispose_Safe()
    {
        var client = new OnvifRecordingClient("http://localhost/onvif/recording");
        client.Dispose();
    }

    [Fact(DisplayName = "OnvifRecordingClient 未连接时调用异常")]
    public async Task GetRecordings_WithoutConnection_Throws()
    {
        using var client = new OnvifRecordingClient("http://192.168.1.99/onvif/recording");
        var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetRecordingsAsync());
        Assert.Contains("canceled", ex.Message);
    }
}
