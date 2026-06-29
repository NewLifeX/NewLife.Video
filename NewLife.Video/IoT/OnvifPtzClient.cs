using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace NewLife.Video.IoT;

/// <summary>ONVIF PTZ 云台控制客户端</summary>
public class OnvifPtzClient : IDisposable
{
    private readonly String _serviceUrl;
    private readonly System.Net.Http.HttpClient _httpClient;

    /// <summary>创建 PTZ 客户端</summary>
    /// <param name="serviceUrl">PTZ 服务地址</param>
    public OnvifPtzClient(String serviceUrl)
    {
        _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        _httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>绝对移动（旋转到指定位置）</summary>
    /// <param name="profileToken">Profile Token</param>
    /// <param name="pan">水平角度 (-1.0 ~ 1.0)</param>
    /// <param name="tilt">垂直角度 (-1.0 ~ 1.0)</param>
    /// <param name="zoom">变焦 (0.0 ~ 1.0)</param>
    public async Task AbsoluteMoveAsync(String profileToken, Double pan, Double tilt, Double zoom)
    {
        var soap = BuildMoveSoap(profileToken, "AbsoluteMove", pan, tilt, zoom);
        await SendSoapAsync(soap);
    }

    /// <summary>连续移动</summary>
    /// <param name="profileToken">Profile Token</param>
    /// <param name="panSpeed">水平速度</param>
    /// <param name="tiltSpeed">垂直速度</param>
    /// <param name="zoomSpeed">变焦速度</param>
    public async Task ContinuousMoveAsync(String profileToken, Double panSpeed, Double tiltSpeed, Double zoomSpeed)
    {
        var soap = BuildMoveSoap(profileToken, "ContinuousMove", panSpeed, tiltSpeed, zoomSpeed);
        await SendSoapAsync(soap);
    }

    /// <summary>停止移动</summary>
    /// <param name="profileToken">Profile Token</param>
    public async Task StopAsync(String profileToken)
    {
        var soap = $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <Stop xmlns=""http://www.onvif.org/ver20/ptz/wsdl"">
      <ProfileToken>{profileToken}</ProfileToken>
      <PanTilt>true</PanTilt><Zoom>true</Zoom>
    </Stop>
  </s:Body>
</s:Envelope>";
        await SendSoapAsync(soap);
    }

    /// <summary>跳转到预置位</summary>
    /// <param name="profileToken">Profile Token</param>
    /// <param name="presetToken">预置位 Token</param>
    public async Task GotoPresetAsync(String profileToken, String presetToken)
    {
        var soap = $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GotoPreset xmlns=""http://www.onvif.org/ver20/ptz/wsdl"">
      <ProfileToken>{profileToken}</ProfileToken>
      <PresetToken>{presetToken}</PresetToken>
    </GotoPreset>
  </s:Body>
</s:Envelope>";
        await SendSoapAsync(soap);
    }

    private String BuildMoveSoap(String token, String moveType, Double pan, Double tilt, Double zoom) =>
        $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <{moveType} xmlns=""http://www.onvif.org/ver20/ptz/wsdl"">
      <ProfileToken>{token}</ProfileToken>
      <Position><PanTilt x=""{pan}"" y=""{tilt}""/><Zoom x=""{zoom}""/></Position>
    </{moveType}>
  </s:Body>
</s:Envelope>";

    private async Task SendSoapAsync(String soap)
    {
        var content = new StringContent(soap, Encoding.UTF8, "application/soap+xml");
        var response = await _httpClient.PostAsync(_serviceUrl, content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>释放资源</summary>
    public void Dispose() => _httpClient?.Dispose();
}
