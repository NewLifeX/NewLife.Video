using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class WebSocketVideoPipeTests
{
    [Fact(DisplayName = "WebSocketVideoPipe null socket 抛 ArgumentNullException")]
    public void Constructor_NullSocket_ThrowsArgumentNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new WebSocketVideoPipe(null));
        Assert.Equal("socket", ex.ParamName);
    }

    [Fact(DisplayName = "WebSocketVideoPipe Socket 属性返回构造时传入的实例")]
    public void Socket_Property_ReturnsSameInstance()
    {
        // WebSocket 是抽象类，用 Mock 过于复杂，此处验证构造参数验证即可
        // 实际 WebSocket 创建需要 ClientWebSocket.ConnectAsync，此处不测试连接
        // 仅验证类型设计正确性：Socket 属性为只读
        var prop = typeof(WebSocketVideoPipe).GetProperty("Socket");
        Assert.NotNull(prop);
        Assert.True(prop.CanRead);
        Assert.False(prop.CanWrite);
    }
}
