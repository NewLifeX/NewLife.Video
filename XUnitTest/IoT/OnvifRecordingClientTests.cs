using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

    [Fact(DisplayName = "OnvifRecordingClient 解析 GetRecordings 响应")]
    public async Task GetRecordings_ValidResponse_ParsesRecordings()
    {
        const String body = """
            <?xml version="1.0" encoding="utf-8"?>
            <Envelope xmlns="http://www.onvif.org/ver10/recording/wsdl">
              <Body>
                <GetRecordingsResponse>
                  <Recording>
                    <RecordingToken>token-1</RecordingToken>
                    <Configuration>
                      <Source>
                        <Name>cam1</Name>
                        <Address>rtsp://cam1/live</Address>
                        <Description>front door</Description>
                      </Source>
                    </Configuration>
                  </Recording>
                </GetRecordingsResponse>
              </Body>
            </Envelope>
            """;

        var recordings = await RequestAsync(200, "OK", body);

        var recording = Assert.Single(recordings);
        Assert.Equal("token-1", recording.Token);
        Assert.Equal("cam1", recording.Name);
        Assert.Equal("rtsp://cam1/live", recording.SourceUri);
        Assert.Equal("front door", recording.Description);
    }

    [Fact(DisplayName = "OnvifRecordingClient 服务错误时抛出异常")]
    public async Task GetRecordings_ServerError_Throws()
    {
        var sw = Stopwatch.StartNew();

        await Assert.ThrowsAsync<HttpRequestException>(() => RequestAsync(500, "Internal Server Error", null));

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"错误响应应快速失败，实际耗时 {sw.Elapsed}");
    }

    private static async Task<OnvifRecording[]> RequestAsync(Int32 statusCode, String reason, String body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = ServeOnceAsync(listener, statusCode, reason, body);

            using var client = new OnvifRecordingClient($"http://127.0.0.1:{port}/onvif/recording");
            var recordings = await client.GetRecordingsAsync();

            await serverTask;
            return recordings;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task ServeOnceAsync(TcpListener listener, Int32 statusCode, String reason, String body)
    {
        using var socket = await listener.AcceptTcpClientAsync();
        using var stream = socket.GetStream();

        // 读取请求头，避免服务端提前关闭导致客户端写入失败
        var buffer = new Byte[4096];
        var received = 0;
        while (received < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer, received, buffer.Length - received);
            if (n <= 0) break;
            received += n;
            if (received >= 4 && buffer.AsSpan(0, received).IndexOf("\r\n\r\n"u8) >= 0) break;
        }

        var bodyBytes = body == null ? [] : Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: application/soap+xml; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await stream.WriteAsync(headerBytes);
        if (bodyBytes.Length > 0) await stream.WriteAsync(bodyBytes);
        await stream.FlushAsync();
    }
}
