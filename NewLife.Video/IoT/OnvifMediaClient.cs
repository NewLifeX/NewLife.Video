using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace NewLife.Video.IoT;

/// <summary>ONVIF 媒体服务客户端。获取摄像头媒体配置（Profile/Stream URI/Snapshot URI）</summary>
public class OnvifMediaClient : IDisposable
{
    private readonly String _serviceUrl;
    private readonly String _username;
    private readonly String _password;
    private readonly HttpClient _httpClient;

    /// <summary>创建 ONVIF 媒体客户端</summary>
    /// <param name="serviceUrl">设备服务地址</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    public OnvifMediaClient(String serviceUrl, String username = null, String password = null)
    {
        _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        _username = username;
        _password = password;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>获取媒体 Profiles</summary>
    /// <returns>Profile 列表</returns>
    public async Task<OnvifProfile[]> GetProfilesAsync()
    {
        var soap = BuildGetProfilesSoap();
        var response = await SendSoapAsync(soap);
        return ParseProfiles(response);
    }

    /// <summary>获取指定 Profile 的 RTSP 流地址</summary>
    /// <param name="profileToken">Profile Token</param>
    /// <returns>RTSP URI</returns>
    public async Task<String> GetStreamUriAsync(String profileToken)
    {
        var soap = BuildGetStreamUriSoap(profileToken);
        var response = await SendSoapAsync(soap);
        return ParseStreamUri(response);
    }

    /// <summary>获取快照 URI</summary>
    /// <param name="profileToken">Profile Token</param>
    /// <returns>快照 URI</returns>
    public async Task<String> GetSnapshotUriAsync(String profileToken)
    {
        var soap = BuildGetSnapshotUriSoap(profileToken);
        var response = await SendSoapAsync(soap);
        return ParseSnapshotUri(response);
    }

    /// <summary>同步获取 Profiles</summary>
    public OnvifProfile[] GetProfiles() => GetProfilesAsync().GetAwaiter().GetResult();

    private String BuildGetProfilesSoap() => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetProfiles xmlns=""http://www.onvif.org/ver10/media/wsdl""/>
  </s:Body>
</s:Envelope>";

    private String BuildGetStreamUriSoap(String token) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetStreamUri xmlns=""http://www.onvif.org/ver10/media/wsdl"">
      <StreamSetup><Stream>RTP-Unicast</Stream><Transport><Protocol>RTSP</Protocol></Transport></StreamSetup>
      <ProfileToken>{token}</ProfileToken>
    </GetStreamUri>
  </s:Body>
</s:Envelope>";

    private String BuildGetSnapshotUriSoap(String token) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetSnapshotUri xmlns=""http://www.onvif.org/ver10/media/wsdl"">
      <ProfileToken>{token}</ProfileToken>
    </GetSnapshotUri>
  </s:Body>
</s:Envelope>";

    private async Task<String> SendSoapAsync(String soap)
    {
        var content = new StringContent(soap, Encoding.UTF8, "application/soap+xml");
        var response = await _httpClient.PostAsync(_serviceUrl, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static OnvifProfile[] ParseProfiles(String xml)
    {
        var profiles = new List<OnvifProfile>();
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();

        foreach (var el in doc.Descendants(ns + "Profiles"))
        {
            profiles.Add(new OnvifProfile
            {
                Token = el.Attribute("token")?.Value,
                Name = el.Element(ns + "Name")?.Value,
                VideoWidth = ParseInt(el.Element(ns + "VideoEncoderConfiguration")?.Element(ns + "Resolution")?.Element(ns + "Width")?.Value),
                VideoHeight = ParseInt(el.Element(ns + "VideoEncoderConfiguration")?.Element(ns + "Resolution")?.Element(ns + "Height")?.Value),
            });
        }

        return profiles.ToArray();
    }

    private static String ParseStreamUri(String xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        return doc.Descendants(ns + "Uri").FirstOrDefault()?.Value;
    }

    private static String ParseSnapshotUri(String xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        return doc.Descendants(ns + "Uri").FirstOrDefault()?.Value;
    }

    private static Int32 ParseInt(String s) => Int32.TryParse(s, out var v) ? v : 0;

    /// <summary>释放资源</summary>
    public void Dispose() => _httpClient?.Dispose();
}

/// <summary>ONVIF Media Profile</summary>
public class OnvifProfile
{
    /// <summary>Profile Token</summary>
    public String Token { get; set; }

    /// <summary>Profile 名称</summary>
    public String Name { get; set; }

    /// <summary>视频宽度</summary>
    public Int32 VideoWidth { get; set; }

    /// <summary>视频高度</summary>
    public Int32 VideoHeight { get; set; }

    /// <summary>字符串表示</summary>
    public override String ToString() => $"{Name} [{Token}] {VideoWidth}x{VideoHeight}";
}
