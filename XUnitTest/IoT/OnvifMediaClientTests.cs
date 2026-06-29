using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class OnvifMediaClientTests
{
    [Fact(DisplayName = "OnvifMediaClient 构造参数验证")]
    public void NullUrl_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OnvifMediaClient(null));
    }

    [Fact(DisplayName = "OnvifProfile ToString正确")]
    public void Profile_ToString_Correct()
    {
        var p = new OnvifProfile { Token = "main", Name = "MainStream", VideoWidth = 1920, VideoHeight = 1080 };
        var s = p.ToString();
        Assert.Contains("MainStream", s);
        Assert.Contains("1920", s);
    }
}
