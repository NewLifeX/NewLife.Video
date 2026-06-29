using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class Gb28181RegistrarTests
{
    [Fact(DisplayName = "Gb28181Registrar 构造正确")]
    public void Constructor_Correct()
    {
        var reg = new Gb28181Registrar("192.168.1.1", 5060, "34020000001110000001", "3402000000");
        Assert.Equal("34020000001110000001", reg.DeviceId);
        Assert.False(reg.IsRegistered);
    }
}
