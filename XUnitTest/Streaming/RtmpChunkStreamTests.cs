using System.Net;
using System.Net.Sockets;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

/// <summary>RTMP 块流读写测试。使用回环 TCP，覆盖分块、扩展 csid 与协议控制消息</summary>
public class RtmpChunkStreamTests
{
    [Fact(DisplayName = "RtmpChunkStream 大消息分块往返一致")]
    public async Task LargePayload_RoundTrip()
    {
        var (sender, receiver, client, server) = await CreatePairAsync();
        using var _ = client;
        using var __ = server;

        var payload = new Byte[300];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (Byte)(i & 0xFF);

        await sender.WriteMessageAsync(3, 1234, 9, 1, payload);

        var message = await receiver.ReadMessageAsync();

        Assert.Equal(9, message.MsgType);
        Assert.Equal(1, message.MsgStreamId);
        Assert.Equal(payload, message.Payload);
    }

    [Fact(DisplayName = "RtmpChunkStream 扩展 csid 往返一致")]
    public async Task ExtendedCsid_RoundTrip()
    {
        var (sender, receiver, client, server) = await CreatePairAsync();
        using var _ = client;
        using var __ = server;

        var first = new Byte[] { 1, 2, 3, 4, 5 };
        var second = new Byte[] { 6, 7, 8, 9, 10, 11 };

        await sender.WriteMessageAsync(70, 0, 18, 2, first);
        await sender.WriteMessageAsync(400, 0, 18, 3, second);

        var message1 = await receiver.ReadMessageAsync();
        var message2 = await receiver.ReadMessageAsync();

        Assert.Equal(2, message1.MsgStreamId);
        Assert.Equal(first, message1.Payload);
        Assert.Equal(3, message2.MsgStreamId);
        Assert.Equal(second, message2.Payload);
    }

    [Fact(DisplayName = "RtmpChunkStream SetChunkSize 后大消息仍可正确读取")]
    public async Task SetChunkSize_ThenLargePayload_RoundTrip()
    {
        var (sender, receiver, client, server) = await CreatePairAsync();
        using var _ = client;
        using var __ = server;

        var payload = new Byte[600];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (Byte)(i * 3 & 0xFF);

        await sender.SendSetChunkSizeAsync(256);
        await sender.WriteMessageAsync(3, 0, 18, 1, payload);

        var message = await receiver.ReadMessageAsync();

        Assert.Equal(18, message.MsgType);
        Assert.Equal(payload, message.Payload);
    }

    [Fact(DisplayName = "RtmpChunkStream 扩展时间戳往返不破坏消息")]
    public async Task ExtendedTimestamp_RoundTrip()
    {
        var (sender, receiver, client, server) = await CreatePairAsync();
        using var _ = client;
        using var __ = server;

        var payload = new Byte[] { 0xAA, 0xBB, 0xCC };

        await sender.WriteMessageAsync(3, 0x01000000, 9, 1, payload);

        var message = await receiver.ReadMessageAsync();

        Assert.Equal(payload, message.Payload);
    }

    [Fact(DisplayName = "RtmpChunkStream 空流抛出参数异常")]
    public void NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RtmpChunkStream(null));
    }

    private static async Task<(RtmpChunkStream Sender, RtmpChunkStream Receiver, TcpClient Client, TcpClient Server)> CreatePairAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new TcpClient { NoDelay = true };
            var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
            var server = await listener.AcceptTcpClientAsync();
            await connectTask;

            return (new RtmpChunkStream(client.GetStream()), new RtmpChunkStream(server.GetStream()), client, server);
        }
        finally
        {
            listener.Stop();
        }
    }
}
