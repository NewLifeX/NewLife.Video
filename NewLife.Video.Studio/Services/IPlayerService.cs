using NewLife.Video.Streaming;

namespace NewLife.Video.Studio.Services;

/// <summary>播放器服务接口</summary>
public interface IPlayerService
{
    /// <summary>当前是否正在播放</summary>
    Boolean IsPlaying { get; }

    /// <summary>当前是否暂停</summary>
    Boolean IsPaused { get; }

    /// <summary>当前播放状态</summary>
    PlayState State { get; }

    /// <summary>视频宽度</summary>
    Int32 VideoWidth { get; }

    /// <summary>视频高度</summary>
    Int32 VideoHeight { get; }

    /// <summary>帧率统计</summary>
    Double FrameRate { get; }

    /// <summary>帧解码事件，参数为 BGRA32 像素数据 (stride = width * 4)</summary>
    event EventHandler<(Byte[] Data, Int32 Width, Int32 Height, Int32 Stride)>? FrameRendered;

    /// <summary>状态变更事件</summary>
    event EventHandler<PlayState>? StateChanged;

    /// <summary>打开视频源</summary>
    /// <param name="url">本地文件路径或 RTSP/RTMP/HLS URL</param>
    void Open(String url);

    /// <summary>开始播放</summary>
    void Play();

    /// <summary>暂停</summary>
    void Pause();

    /// <summary>停止</summary>
    void Stop();
}
