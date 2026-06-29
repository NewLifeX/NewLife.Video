using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class Gb28181PlayerTests
{
    [Fact(DisplayName = "Gb28181Player 构造正确")]
    public void Constructor_Correct()
    {
        var player = new Gb28181Player("192.168.1.1", 5060, "34020000001110000001", "3402000000");
        Assert.Equal("34020000001110000001", player.DeviceId);
        Assert.False(player.IsPlaying);
    }
}
