using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace NewLife.Video.IoT;

/// <summary>ONVIF Recording 服务客户端（Profile G 边缘存储）。管理摄像头端录像录制、查询和删除</summary>
/// <remarks>
/// 基于 ONVIF Recording Service (ver10) 规范，通过 SOAP/XML 与摄像头通信。
/// 使用示例：
/// <code>
/// using var client = new OnvifRecordingClient("http://camera/onvif/recording", "admin", "12345");
/// var recordings = client.GetRecordings();
/// var job = client.CreateRecording("MyRecording", "rtsp://.../stream");
/// </code>
/// </remarks>
public class OnvifRecordingClient : IDisposable
{
    private readonly String _serviceUrl;
    private readonly String _username;
    private readonly String _password;
    private readonly HttpClient _httpClient;

    /// <summary>创建 ONVIF Recording 客户端</summary>
    /// <param name="serviceUrl">Recording Service 地址（如 http://camera/onvif/recording）</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    public OnvifRecordingClient(String serviceUrl, String username = null, String password = null)
    {
        _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        _username = username;
        _password = password;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>获取所有录像列表</summary>
    /// <returns>录像记录列表</returns>
    public async Task<OnvifRecording[]> GetRecordingsAsync()
    {
        var soap = BuildGetRecordingsSoap();
        var response = await SendSoapAsync(soap);
        return ParseRecordings(response);
    }

    /// <summary>创建新录像</summary>
    /// <param name="name">录像名称</param>
    /// <param name="sourceRtspUri">视频源 RTSP 地址</param>
    /// <returns>创建的录像记录</returns>
    public async Task<OnvifRecording> CreateRecordingAsync(String name, String sourceRtspUri)
    {
        var soap = BuildCreateRecordingSoap(name, sourceRtspUri);
        var response = await SendSoapAsync(soap);
        return ParseRecording(response, name);
    }

    /// <summary>删除录像</summary>
    /// <param name="recordingToken">录像 Token</param>
    /// <returns>是否成功</returns>
    public async Task<Boolean> DeleteRecordingAsync(String recordingToken)
    {
        var soap = BuildDeleteRecordingSoap(recordingToken);
        await SendSoapAsync(soap);
        return true;
    }

    /// <summary>获取录像配置选项</summary>
    /// <returns>录像选项（如最大持续时间、支持的录制模式等）</returns>
    public async Task<OnvifRecordingOptions> GetRecordingOptionsAsync()
    {
        var soap = BuildGetRecordingOptionsSoap();
        var response = await SendSoapAsync(soap);
        return ParseRecordingOptions(response);
    }

    /// <summary>获取录像任务列表</summary>
    public async Task<OnvifRecordingJob[]> GetRecordingJobsAsync()
    {
        var soap = BuildGetRecordingJobsSoap();
        var response = await SendSoapAsync(soap);
        return ParseRecordingJobs(response);
    }

    /// <summary>创建录像任务（关联录像源和存储）</summary>
    /// <param name="recordingToken">录像 Token</param>
    /// <param name="sourceToken">视频源 Profile Token</param>
    /// <returns>录像任务</returns>
    public async Task<OnvifRecordingJob> CreateRecordingJobAsync(String recordingToken, String sourceToken)
    {
        var soap = BuildCreateRecordingJobSoap(recordingToken, sourceToken);
        var response = await SendSoapAsync(soap);
        return ParseRecordingJob(response, recordingToken);
    }

    /// <summary>删除录像任务</summary>
    public async Task<Boolean> DeleteRecordingJobAsync(String jobToken)
    {
        var soap = BuildDeleteRecordingJobSoap(jobToken);
        await SendSoapAsync(soap);
        return true;
    }

    /// <summary>获取回放 URI（Profile G 回放）</summary>
    /// <param name="recordingToken">录像 Token</param>
    /// <returns>RTSP 回放地址</returns>
    public async Task<String> GetReplayUriAsync(String recordingToken)
    {
        var soap = BuildGetReplayUriSoap(recordingToken);
        var response = await SendSoapAsync(soap);
        return ParseReplayUri(response);
    }

    // 同步包装
    public OnvifRecording[] GetRecordings() => GetRecordingsAsync().GetAwaiter().GetResult();
    public OnvifRecording CreateRecording(String name, String sourceRtspUri) => CreateRecordingAsync(name, sourceRtspUri).GetAwaiter().GetResult();
    public Boolean DeleteRecording(String recordingToken) => DeleteRecordingAsync(recordingToken).GetAwaiter().GetResult();
    public OnvifRecordingJob[] GetRecordingJobs() => GetRecordingJobsAsync().GetAwaiter().GetResult();
    public OnvifRecordingJob CreateRecordingJob(String recordingToken, String sourceToken) => CreateRecordingJobAsync(recordingToken, sourceToken).GetAwaiter().GetResult();
    public Boolean DeleteRecordingJob(String jobToken) => DeleteRecordingJobAsync(jobToken).GetAwaiter().GetResult();
    public String GetReplayUri(String recordingToken) => GetReplayUriAsync(recordingToken).GetAwaiter().GetResult();

    #region SOAP 构建

    private String BuildGetRecordingsSoap() => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetRecordings xmlns=""http://www.onvif.org/ver10/recording/wsdl""/>
  </s:Body>
</s:Envelope>";

    private String BuildCreateRecordingSoap(String name, String sourceUri) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <CreateRecording xmlns=""http://www.onvif.org/ver10/recording/wsdl"">
      <RecordingConfiguration>
        <Source>
          <SourceId>{EscapeXml(name)}</SourceId>
          <Name>{EscapeXml(name)}</Name>
          <Location>Camera</Location>
          <Description>{EscapeXml(name)}</Description>
          <Address>{EscapeXml(sourceUri)}</Address>
        </Source>
        <Content></Content>
        <MaximumRetentionTime>PT1H</MaximumRetentionTime>
      </RecordingConfiguration>
    </CreateRecording>
  </s:Body>
</s:Envelope>";

    private String BuildDeleteRecordingSoap(String token) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <DeleteRecording xmlns=""http://www.onvif.org/ver10/recording/wsdl"">
      <RecordingToken>{EscapeXml(token)}</RecordingToken>
    </DeleteRecording>
  </s:Body>
</s:Envelope>";

    private String BuildGetRecordingOptionsSoap() => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetRecordingOptions xmlns=""http://www.onvif.org/ver10/recording/wsdl""/>
  </s:Body>
</s:Envelope>";

    private String BuildGetRecordingJobsSoap() => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetRecordingJobs xmlns=""http://www.onvif.org/ver10/recording/wsdl""/>
  </s:Body>
</s:Envelope>";

    private String BuildCreateRecordingJobSoap(String recordingToken, String sourceToken) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <CreateRecordingJob xmlns=""http://www.onvif.org/ver10/recording/wsdl"">
      <JobConfiguration>
        <RecordingToken>{EscapeXml(recordingToken)}</RecordingToken>
        <Mode>Active</Mode>
        <Priority>Medium</Priority>
        <Source>
          <SourceToken>{EscapeXml(sourceToken)}</SourceToken>
          <Type>VideoSource</Type>
        </Source>
        <FixedDuration>PT1H</FixedDuration>
      </JobConfiguration>
    </CreateRecordingJob>
  </s:Body>
</s:Envelope>";

    private String BuildDeleteRecordingJobSoap(String jobToken) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <DeleteRecordingJob xmlns=""http://www.onvif.org/ver10/recording/wsdl"">
      <JobToken>{EscapeXml(jobToken)}</JobToken>
    </DeleteRecordingJob>
  </s:Body>
</s:Envelope>";

    private String BuildGetReplayUriSoap(String recordingToken) => $@"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
  <s:Body>
    <GetReplayUri xmlns=""http://www.onvif.org/ver10/replay/wsdl"">
      <RecordingToken>{EscapeXml(recordingToken)}</RecordingToken>
      <StreamSetup>
        <Stream>RTP-Unicast</Stream>
        <Transport><Protocol>RTSP</Protocol></Transport>
      </StreamSetup>
    </GetReplayUri>
  </s:Body>
</s:Envelope>";

    #endregion

    #region 辅助

    private async Task<String> SendSoapAsync(String soap)
    {
        var content = new StringContent(soap, Encoding.UTF8, "application/soap+xml");
        if (!String.IsNullOrEmpty(_username) && !String.IsNullOrEmpty(_password))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            content.Headers.Add("Authorization", $"Basic {auth}");
        }
        var response = await _httpClient.PostAsync(_serviceUrl, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static OnvifRecording[] ParseRecordings(String xml)
    {
        var list = new List<OnvifRecording>();
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();

        foreach (var el in doc.Descendants(ns + "Recording"))
        {
            var token = el.Element(ns + "RecordingToken")?.Value;
            var config = el.Element(ns + "Configuration");
            var source = config?.Element(ns + "Source");
            list.Add(new OnvifRecording
            {
                Token = token,
                Name = source?.Element(ns + "Name")?.Value,
                SourceUri = source?.Element(ns + "Address")?.Value,
                Description = source?.Element(ns + "Description")?.Value,
            });
        }
        return list.ToArray();
    }

    private static OnvifRecording ParseRecording(String xml, String defaultName)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        var token = doc.Descendants(ns + "RecordingToken").FirstOrDefault()?.Value;
        return new OnvifRecording { Token = token, Name = defaultName };
    }

    private static OnvifRecordingOptions ParseRecordingOptions(String xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        return new OnvifRecordingOptions
        {
            MaxRecordings = ParseInt(doc.Descendants(ns + "MaxRecordings").FirstOrDefault()?.Value),
            MaxRetentionTime = doc.Descendants(ns + "MaxRetentionTime").FirstOrDefault()?.Value,
        };
    }

    private static OnvifRecordingJob[] ParseRecordingJobs(String xml)
    {
        var list = new List<OnvifRecordingJob>();
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();

        foreach (var el in doc.Descendants(ns + "RecordingJob"))
        {
            var token = el.Element(ns + "JobToken")?.Value;
            var status = el.Element(ns + "JobState")?.Value;
            var config = el.Element(ns + "Configuration");
            list.Add(new OnvifRecordingJob
            {
                Token = token,
                Status = status,
                RecordingToken = config?.Element(ns + "RecordingToken")?.Value,
            });
        }
        return list.ToArray();
    }

    private static OnvifRecordingJob ParseRecordingJob(String xml, String recToken)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        var token = doc.Descendants(ns + "JobToken").FirstOrDefault()?.Value;
        return new OnvifRecordingJob { Token = token, RecordingToken = recToken, Status = "Active" };
    }

    private static String ParseReplayUri(String xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root.GetDefaultNamespace();
        return doc.Descendants(ns + "Uri").FirstOrDefault()?.Value;
    }

    private static String EscapeXml(String s) => s?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static Int32 ParseInt(String s) => Int32.TryParse(s, out var v) ? v : 0;

    /// <summary>释放资源</summary>
    public void Dispose() => _httpClient?.Dispose();

    #endregion
}

/// <summary>ONVIF 录像记录</summary>
public class OnvifRecording
{
    /// <summary>录像 Token</summary>
    public String Token { get; set; }

    /// <summary>录像名称</summary>
    public String Name { get; set; }

    /// <summary>视频源 RTSP URI</summary>
    public String SourceUri { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }
}

/// <summary>ONVIF 录像选项</summary>
public class OnvifRecordingOptions
{
    /// <summary>最大录像数</summary>
    public Int32 MaxRecordings { get; set; }

    /// <summary>最大保留时间</summary>
    public String MaxRetentionTime { get; set; }
}

/// <summary>ONVIF 录像任务</summary>
public class OnvifRecordingJob
{
    /// <summary>任务 Token</summary>
    public String Token { get; set; }

    /// <summary>关联的录像 Token</summary>
    public String RecordingToken { get; set; }

    /// <summary>任务状态（Active/Inactive/Error）</summary>
    public String Status { get; set; }
}
