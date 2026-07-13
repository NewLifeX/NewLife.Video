using NewLife.Log;
using NewLife.Video.Processing;
using NewLife.Video.Streaming;

namespace NewLife.Video.Studio.Services;

/// <summary>播放器服务，封装 FfmpegPlayer + 帧渲染</summary>
public class PlayerService : IPlayerService, IDisposable
{
    private FfmpegPlayer? _player;
    private PlayState _currentState = PlayState.Stopped;
    private Boolean _disposed;

    /// <summary>帧解码事件，参数为 BGRA32 像素数据</summary>
    public event EventHandler<(Byte[] Data, Int32 Width, Int32 Height, Int32 Stride)>? FrameRendered;

    /// <summary>状态变更事件</summary>
    public event EventHandler<PlayState>? StateChanged;

    /// <summary>当前是否正在播放</summary>
    public Boolean IsPlaying => _currentState == PlayState.Playing;

    /// <summary>当前是否暂停</summary>
    public Boolean IsPaused => _currentState == PlayState.Paused;

    /// <summary>当前播放状态</summary>
    public PlayState State => _currentState;

    /// <summary>视频宽度</summary>
    public Int32 VideoWidth { get; private set; }

    /// <summary>视频高度</summary>
    public Int32 VideoHeight { get; private set; }

    /// <summary>帧率统计</summary>
    public Double FrameRate => _player?.FrameRate ?? 0;

    /// <summary>打开视频源</summary>
    public void Open(String url)
    {
        Stop();

        _player = new FfmpegPlayer();
        _player.StateChanged += OnPlayerStateChanged;
        _player.FrameDecoded += OnFrameDecoded;
        _player.Open(url);

        VideoWidth = _player.Width;
        VideoHeight = _player.Height;
    }

    /// <summary>开始播放</summary>
    public void Play()
    {
        if (_player == null) return;
        if (_currentState == PlayState.Paused)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    /// <summary>暂停</summary>
    public void Pause()
    {
        if (_player == null) return;
        _player.Pause();
    }

    /// <summary>停止</summary>
    public void Stop()
    {
        if (_player != null)
        {
            _player.StateChanged -= OnPlayerStateChanged;
            _player.FrameDecoded -= OnFrameDecoded;
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
        _currentState = PlayState.Stopped;
        VideoWidth = 0;
        VideoHeight = 0;
    }

    private void OnPlayerStateChanged(Object? sender, PlayState state)
    {
        _currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnFrameDecoded(Object? sender, VideoFrame frame)
    {
        try
        {
            var bgra = VideoFrameRenderer.ToBgra32(frame);
            FrameRendered?.Invoke(this, (bgra, frame.Width, frame.Height, frame.Width * 4));
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
