using LibVLCSharp.Shared;
using NewLife.Video.Containers;

namespace NewLife.Video.Streaming;

/// <summary>LibVLCSharp 集成封装。提供统一播放 API，支持 RTSP/RTMP/HLS/DASH 一键播放</summary>
/// <remarks>
/// 封装 LibVLCSharp 的 MediaPlayer，提供与 NewLife.Video 一致的接口抽象，
/// 支持多种协议和 UI 框架（WPF/WinForms/MAUI/Avalonia）。
/// 
/// 使用示例：
/// <code>
/// var player = new VlcPlayer();
/// player.Play(new Uri("rtsp://camera/stream"));
/// player.Pause();
/// player.Stop();
/// </code>
/// </remarks>
public class VlcPlayer : IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    /// <summary>是否正在播放</summary>
    public Boolean IsPlaying => _mediaPlayer?.IsPlaying ?? false;

    /// <summary>当前播放位置（毫秒）</summary>
    public Int64 Position => _mediaPlayer?.Time ?? 0;

    /// <summary>媒体时长（毫秒）</summary>
    public Int64 Duration => _mediaPlayer?.Length ?? 0;

    /// <summary>音量 0-100</summary>
    public Int32 Volume
    {
        get => _mediaPlayer?.Volume ?? 50;
        set { if (_mediaPlayer != null) _mediaPlayer.Volume = value; }
    }

    /// <summary>播放状态变化事件</summary>
    public event EventHandler<VlcPlayState> StateChanged;

    /// <summary>创建 VLC 播放器</summary>
    /// <param name="options">LibVLC 初始化参数（如 ["--no-audio", "--network-caching=300"]）</param>
    public VlcPlayer(String[] options = null)
    {
        Core.Initialize();
        _libVlc = new LibVLC(options ?? ["--network-caching=300", "--no-xlib"]);
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.Playing += (s, e) => StateChanged?.Invoke(this, VlcPlayState.Playing);
        _mediaPlayer.Paused += (s, e) => StateChanged?.Invoke(this, VlcPlayState.Paused);
        _mediaPlayer.Stopped += (s, e) => StateChanged?.Invoke(this, VlcPlayState.Stopped);
        _mediaPlayer.EndReached += (s, e) => StateChanged?.Invoke(this, VlcPlayState.Ended);
    }

    /// <summary>播放指定 URI</summary>
    /// <param name="uri">媒体 URI（支持 rtsp://, rtmp://, http://hls, http://dash 等）</param>
    public void Play(Uri uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        using var media = new Media(_libVlc, uri);
        _mediaPlayer.Play(media);
    }

    /// <summary>播放指定文件</summary>
    /// <param name="filePath">本地文件路径</param>
    public void PlayFile(String filePath)
    {
        if (String.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        using var media = new Media(_libVlc, filePath);
        _mediaPlayer.Play(media);
    }

    /// <summary>暂停</summary>
    public void Pause() => _mediaPlayer?.Pause();

    /// <summary>停止</summary>
    public void Stop() => _mediaPlayer?.Stop();

    /// <summary>跳转到指定位置</summary>
    /// <param name="timeMs">目标时间（毫秒）</param>
    public void Seek(Int64 timeMs) { if (_mediaPlayer != null) _mediaPlayer.Time = timeMs; }

    /// <summary>设置播放速率</summary>
    public void SetRate(Single rate) { if (_mediaPlayer != null) _mediaPlayer.Rate = rate; }

    /// <summary>获取 WPF 控件宿主</summary>
    public Object GetWpfHost()
    {
        var provider = _mediaPlayer.WeakReference;
        return new LibVLCSharp.WPF.MediaPlayerElement { MediaPlayer = provider };
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}

/// <summary>VLC 播放状态</summary>
public enum VlcPlayState
{
    /// <summary>播放中</summary>
    Playing,
    /// <summary>已暂停</summary>
    Paused,
    /// <summary>已停止</summary>
    Stopped,
    /// <summary>播放结束</summary>
    Ended,
}
