using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class OnvifPtzClientTests
{
    [Fact(DisplayName = "OnvifPtzClient 构造参数验证")]
    public void NullUrl_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OnvifPtzClient(null));
    }

    [Fact(DisplayName = "OnvifPtzClient 可正常构造")]
    public void Constructs()
    {
        using var client = new OnvifPtzClient("http://192.168.1.1/onvif/ptz");
        Assert.NotNull(client);
    }
}
