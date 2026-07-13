using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Streaming;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>流媒体视图模型（M5-1 RTSP/RTMP）</summary>
public partial class StreamingViewModel : ViewModelBase
{
    [ObservableProperty]
    private String _rtspUrl = "rtsp://";

    [ObservableProperty]
    private String _rtmpUrl = "rtmp://";

    [ObservableProperty]
    private String _statusText = "就绪";

    [ObservableProperty]
    private Boolean _isConnected;

    /// <summary>RTSP 拉流测试</summary>
    [RelayCommand]
    private async Task TestRtspPull()
    {
        if (String.IsNullOrWhiteSpace(RtspUrl)) { StatusText = "请输入 RTSP 地址"; return; }

        try
        {
            StatusText = "正在连接 RTSP...";
            using var client = new RtspClient(RtspUrl);

            if (await client.ConnectAsync())
                StatusText = $"RTSP 连接成功: {client.Sdp?.Length ?? 0} 字节 SDP";
            else
                StatusText = "RTSP 连接失败";
        }
        catch (Exception ex)
        {
            StatusText = $"RTSP 错误: {ex.Message}";
        }
    }

    /// <summary>RTMP 推流测试</summary>
    [RelayCommand]
    private async Task TestRtmpPush()
    {
        if (String.IsNullOrWhiteSpace(RtmpUrl)) { StatusText = "请输入 RTMP 地址"; return; }

        try
        {
            StatusText = "正在连接 RTMP...";
            using var client = new RtmpClient(RtmpUrl);

            if (await client.ConnectAsync())
            {
                if (await client.ConnectStreamAsync())
                {
                    var streamId = await client.CreateStreamAsync();
                    if (streamId > 0 && await client.PublishAsync())
                    {
                        StatusText = $"RTMP 推流就绪 (Stream ID: {streamId})";
                        IsConnected = true;
                        return;
                    }
                }
                StatusText = "RTMP 推流准备失败";
            }
            else
            {
                StatusText = "RTMP 连接失败";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"RTMP 错误: {ex.Message}";
        }
        IsConnected = false;
    }

    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        StatusText = "已断开连接";
    }
}
